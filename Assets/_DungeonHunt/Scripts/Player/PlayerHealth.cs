using System;
using System.Collections.Generic;
using UnityEngine;

public enum PlayerLifeState
{
    Vulnerable,
    HitInvincible,
    Dead,
}

// 몬스터 공격 한 건. 같은 처리 틱에 모았다가 LateUpdate에서 한 번에 확정함 (01-3 §4.1).
public struct IncomingAttack
{
    public Monster Attacker;   // null이면 주체 없음 (보스 등은 별도 Owner)
    public UnityEngine.Object Owner;
    public int Damage;
    public bool IsContact;
    public int AttackGroupId;  // 0이 아니면 같은 값의 공격에서 저스트회피 1회만 (05 §8.3)
}

// 01-3 생존. 현재 HP, 피격 후 무적, 사망을 소유함. 최대 HP는 01-4 최종값.
[DefaultExecutionOrder(50)]
public class PlayerHealth : MonoBehaviour
{
    public int CurrentHP { get; private set; }
    public int MaxHP => stats != null ? stats.GetInt(StatId.MaxHP) : GameBalance.Current.Player.MaxHP;
    public bool IsDead => State == PlayerLifeState.Dead;
    public PlayerLifeState State { get; private set; }
    public bool IsHitInvincible => State == PlayerLifeState.HitInvincible;
    // 효과 무적 (회피 세트 Tier2 저스트회피 무적 등). 몬스터는 계속 노리지만 피해는 안 받음
    public bool IsEffectInvincible => GameClock.CombatTime < effectInvincibleUntil;
    private float effectInvincibleUntil;

    public void GrantInvincibility(float duration)
    {
        effectInvincibleUntil = Mathf.Max(effectInvincibleUntil, GameClock.CombatTime + duration);
    }

    // 기존 UI 호환
    public event Action<int> HPChanged;
    public event Action Died;

    private readonly List<IncomingAttack> pending = new List<IncomingAttack>();
    private readonly HashSet<int> justDodgedGroups = new HashSet<int>();
    private float hitInvincibleUntil;
    private PlayerMovement movement;
    private PlayerStats stats;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        stats = GetComponent<PlayerStats>();
        if (stats == null) stats = gameObject.AddComponent<PlayerStats>();
        CurrentHP = MaxHP;
    }

    private void OnEnable() => GameEvents.EffectiveStatChanged += OnStatChanged;
    private void OnDisable() => GameEvents.EffectiveStatChanged -= OnStatChanged;

    // 런 시작: 항상 풀피 (01-3 §6)
    public void ResetForNewRun()
    {
        pending.Clear();
        justDodgedGroups.Clear();
        State = PlayerLifeState.Vulnerable;
        hitInvincibleUntil = 0f;
        effectInvincibleUntil = 0f;
        SetHP(MaxHP);
    }

    // 피격 무적이 끝난 틱은 충돌 수집 전에 Vulnerable (01-3 §7.2)
    private void Update()
    {
        if (State == PlayerLifeState.HitInvincible && GameClock.CombatTime >= hitInvincibleUntil)
            State = PlayerLifeState.Vulnerable;
    }

    // 공격이 플레이어 피격 판정에 닿음. 반환: 무적으로 막혔으면 true (적 탄은 어느 쪽이든 소멸).
    public bool ReceiveAttack(IncomingAttack attack)
    {
        if (IsDead) return true;

        bool dodgeInvincible = movement != null && movement.IsDodgeInvincible;
        // 저스트회피는 접촉 정보를 먼저 씀. 몬스터 접촉 피해는 대상 아님 (04 §3.1)
        bool groupSpent = attack.AttackGroupId != 0 && justDodgedGroups.Contains(attack.AttackGroupId);
        if (!attack.IsContact && dodgeInvincible && !groupSpent && movement.TryTriggerJustDodge())
        {
            if (attack.AttackGroupId != 0) justDodgedGroups.Add(attack.AttackGroupId);
            GetComponent<WeaponController>()?.OnJustDodge();
        }

        if (dodgeInvincible || IsHitInvincible || IsEffectInvincible) return true;

        pending.Add(attack);
        return false;
    }

    // 같은 처리 틱 피해 확정: 사망한 주체 공격 제거 → 같은 몬스터는 능동 공격 우선 → 합산 1회 적용
    private void LateUpdate()
    {
        if (pending.Count == 0) return;
        if (IsDead || IsHitInvincible || IsEffectInvincible || (movement != null && movement.IsDodgeInvincible))
        {
            pending.Clear();
            return;
        }

        var activeAttackers = new HashSet<UnityEngine.Object>();
        foreach (IncomingAttack attack in pending)
        {
            if (!attack.IsContact && attack.Owner != null)
                activeAttackers.Add(attack.Owner);
        }

        int total = 0;
        foreach (IncomingAttack attack in pending)
        {
            if (attack.Attacker != null && attack.Attacker.IsDead) continue;
            if (attack.IsContact && attack.Owner != null && activeAttackers.Contains(attack.Owner)) continue;
            total += attack.Damage;
        }
        pending.Clear();

        if (total <= 0) return;
        ApplyDamage(total);
    }

    private void ApplyDamage(int amount)
    {
        SetHP(Mathf.Max(0, CurrentHP - amount));
        GameEvents.Raise(GameEvents.PlayerHit, amount);
        GameServices.Feedback?.OnPlayerHit();

        if (CurrentHP <= 0)
        {
            State = PlayerLifeState.Dead;
            Died?.Invoke();
            GameEvents.Raise(GameEvents.PlayerDied);
            return;
        }

        State = PlayerLifeState.HitInvincible;
        hitInvincibleUntil = GameClock.CombatTime + (stats != null ? stats.Get(StatId.HitInvincibleDuration) : GameBalance.Current.Player.HitInvincibilityDuration);
    }

    // HP 회복. 최대치까지만, 실제 회복량을 돌려줌.
    public int Heal(int amount)
    {
        if (IsDead || amount <= 0) return 0;
        int before = CurrentHP;
        SetHP(Mathf.Min(MaxHP, CurrentHP + amount));
        return CurrentHP - before;
    }

    public bool IsFull => CurrentHP >= MaxHP;

    private void SetHP(int value)
    {
        CurrentHP = value;
        HPChanged?.Invoke(CurrentHP);
        GameEvents.Raise(GameEvents.PlayerHPChanged, CurrentHP, MaxHP);
    }

    // 최대치 감소 시 현재값 제한, 증가 시 현재값 그대로 (01-4 §7)
    private void OnStatChanged(StatId stat, float previous, float value)
    {
        if (stat != StatId.MaxHP) return;
        if (CurrentHP > MaxHP) SetHP(MaxHP);
        else GameEvents.Raise(GameEvents.PlayerHPChanged, CurrentHP, MaxHP);
    }
}
