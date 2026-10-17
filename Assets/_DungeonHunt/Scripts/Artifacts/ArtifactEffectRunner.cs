using System.Collections.Generic;
using UnityEngine;

// 아티팩트/세트 특수 효과 실행 (스탯 수정값으로 못 하는 것). 플레이어에 붙음.
// 장착 아티팩트 + 활성 세트 Tier의 효과를 매 프레임 합산하고(같은 종류는 합연산, 교체 직후 배율만 곱연산),
// 게임 이벤트에 맞춰 시간제 스탯 원천을 켜고 끄거나 무기 상태를 바꿈.
// 시간은 전투 시계 기준이라 히트스톱과 일시정지 동안 멈춤.
public class ArtifactEffectRunner : MonoBehaviour
{
    private const string SwapWindowSource = "Effect:SwapWindow";
    private const string DodgeBuffSource = "Effect:DodgeBuff";
    private const string RifleMoveSource = "Effect:RifleFiringMove";
    private const string EnhancedSourcePrefix = "Effect:Enhanced:";

    public static ArtifactEffectRunner Instance { get; private set; }

    // 합산 결과
    private float swapWindowMultiplier = 1f, swapWindowDuration;
    private float ricochetChance;
    private float dodgeBuffPercent, dodgeBuffDuration;
    private float justDodgeInvincible;
    private float rifleStackPercent;
    private float rifleMovePercent;
    private float enhancedPercent;
    private float refillRatio;

    // 진행 상태
    private float swapWindowUntil = -1f;
    private float dodgeBuffUntil = -1f;
    private bool rifleMoveApplied;
    private readonly Dictionary<WeaponType, float> enhancedApplied = new Dictionary<WeaponType, float>();
    private object rifleStackTarget;
    private int rifleStacks;
    private float rifleStackUntil = -1f;

    private readonly List<ArtifactEffect> buffer = new List<ArtifactEffect>();
    private PlayerController player;

    public float PistolRicochetChance => ricochetChance;
    public bool HasRifleStackEffect => rifleStackPercent > 0f;
    public int RifleStacks => GameClock.CombatTime < rifleStackUntil ? rifleStacks : 0;
    public float RifleStackRemaining01 => RifleStacks > 0 ? Mathf.Clamp01((rifleStackUntil - GameClock.CombatTime) / Mathf.Max(0.01f, GameBalance.Current.Artifact.RifleStackDuration)) : 0f;

    private void Awake()
    {
        Instance = this;
        player = GetComponent<PlayerController>();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnEnable()
    {
        GameEvents.DodgeStart += OnDodgeStart;
        GameEvents.JustDodgeTriggered += OnJustDodge;
        GameEvents.RunStarted += ResetState;
    }

    private void OnDisable()
    {
        GameEvents.DodgeStart -= OnDodgeStart;
        GameEvents.JustDodgeTriggered -= OnJustDodge;
        GameEvents.RunStarted -= ResetState;
        if (player != null && player.Weapons != null) player.Weapons.ManualSwapPerformed -= OnManualSwap;
    }

    private void Start()
    {
        if (player != null && player.Weapons != null) player.Weapons.ManualSwapPerformed += OnManualSwap;
    }

    // 런 시작 시 스탯 원천은 PlayerStats.ClearRunSources가 지우므로 상태만 초기화
    private void ResetState()
    {
        swapWindowUntil = dodgeBuffUntil = rifleStackUntil = -1f;
        rifleMoveApplied = false;
        enhancedApplied.Clear();
        rifleStackTarget = null;
        rifleStacks = 0;
    }

    private void Update()
    {
        Recalculate();
        if (player == null) return;
        PlayerStats stats = player.Stats;
        float now = GameClock.CombatTime;

        if (swapWindowUntil >= 0f && now >= swapWindowUntil)
        {
            swapWindowUntil = -1f;
            stats.RemoveSource(SwapWindowSource);
        }
        if (dodgeBuffUntil >= 0f && now >= dodgeBuffUntil)
        {
            dodgeBuffUntil = -1f;
            stats.RemoveSource(DodgeBuffSource);
        }

        // 라이플 사격 중 이동속도
        bool rifleMove = rifleMovePercent > 0f && player.Weapons.IsRifleFiring;
        if (rifleMove != rifleMoveApplied)
        {
            rifleMoveApplied = rifleMove;
            if (rifleMove) stats.SetSource(RifleMoveSource, new StatModifier(StatId.MoveSpeed, ModifierKind.Percent, rifleMovePercent));
            else stats.RemoveSource(RifleMoveSource);
        }

        // 강화탄: 강화된 무기의 피해 비율 합에 더함 (01-2 §8.3 같은 피해 스탯의 비율 수정값)
        foreach (WeaponRuntime weapon in player.Weapons.Slots)
        {
            float want = weapon.IsEnhanced && enhancedPercent > 0f ? enhancedPercent : 0f;
            enhancedApplied.TryGetValue(weapon.Type, out float have);
            if (Mathf.Approximately(want, have)) continue;
            enhancedApplied[weapon.Type] = want;
            string source = EnhancedSourcePrefix + weapon.Type;
            if (want > 0f) stats.SetSource(source, new StatModifier(StatNames.ForWeapon(weapon.Type, WeaponStat.Damage).specific, ModifierKind.Percent, want));
            else stats.RemoveSource(source);
        }
        if (enhancedPercent <= 0f)
            foreach (WeaponRuntime weapon in player.Weapons.Slots) weapon.IsEnhanced = false;

        if (rifleStacks > 0 && now >= rifleStackUntil)
        {
            rifleStacks = 0;
            rifleStackTarget = null;
        }
    }

    private void Recalculate()
    {
        swapWindowMultiplier = 1f; swapWindowDuration = 0f;
        ricochetChance = dodgeBuffPercent = dodgeBuffDuration = justDodgeInvincible = 0f;
        rifleStackPercent = rifleMovePercent = enhancedPercent = refillRatio = 0f;
        ArtifactSystem artifacts = GameServices.Artifacts;
        if (artifacts == null) return;
        artifacts.CollectEffects(buffer);
        foreach (ArtifactEffect e in buffer)
        {
            switch (e.Kind)
            {
                case ArtifactEffectKind.SwapDamageWindow:
                    swapWindowMultiplier *= Mathf.Max(0f, e.Value);
                    swapWindowDuration = Mathf.Max(swapWindowDuration, e.Duration);
                    break;
                case ArtifactEffectKind.PistolRicochet: ricochetChance += e.Value; break;
                case ArtifactEffectKind.DodgeDamageBuff:
                    dodgeBuffPercent += e.Value;
                    dodgeBuffDuration = Mathf.Max(dodgeBuffDuration, e.Duration);
                    break;
                case ArtifactEffectKind.JustDodgeInvincible: justDodgeInvincible = Mathf.Max(justDodgeInvincible, e.Duration); break;
                case ArtifactEffectKind.RifleStackDamage: rifleStackPercent += e.Value; break;
                case ArtifactEffectKind.RifleFiringMoveSpeed: rifleMovePercent += e.Value; break;
                case ArtifactEffectKind.SwapEnhancedRounds: enhancedPercent += e.Value; break;
                case ArtifactEffectKind.SwapRefill: refillRatio += e.Value; break;
            }
        }
        ricochetChance = Mathf.Clamp01(ricochetChance);
        refillRatio = Mathf.Clamp01(refillRatio);
    }

    // ---------- 이벤트 ----------

    // 수동 교체(Q/E)만. 특화 강제 전환은 제외 (01-1 §3 WeaponSwapped 비고)
    private void OnManualSwap(WeaponType type)
    {
        Recalculate();
        WeaponRuntime weapon = player.Weapons.Get(type);
        if (weapon == null) return;

        if (swapWindowDuration > 0f && !Mathf.Approximately(swapWindowMultiplier, 1f))
        {
            swapWindowUntil = GameClock.CombatTime + swapWindowDuration;
            player.Stats.SetSource(SwapWindowSource, new StatModifier(StatId.WeaponDamageAll, ModifierKind.Multiplier, swapWindowMultiplier));
        }

        // 탄창 회복 먼저, 그다음 탄창 전체를 강화탄으로
        if (refillRatio > 0f)
        {
            weapon.RefundAmmo(Mathf.CeilToInt(weapon.MaxAmmo * refillRatio));
            if (weapon.IsReloading && weapon.Ammo >= weapon.MaxAmmo) weapon.CancelReload();
        }
        if (enhancedPercent > 0f) weapon.IsEnhanced = true;
    }

    private void OnDodgeStart()
    {
        Recalculate();
        if (dodgeBuffPercent <= 0f || dodgeBuffDuration <= 0f || player == null) return;
        dodgeBuffUntil = GameClock.CombatTime + dodgeBuffDuration;
        player.Stats.SetSource(DodgeBuffSource, new StatModifier(StatId.WeaponDamageAll, ModifierKind.Percent, dodgeBuffPercent));
    }

    private void OnJustDodge()
    {
        Recalculate();
        if (justDodgeInvincible > 0f && player != null) player.Health.GrantInvincibility(justDodgeInvincible);
    }

    // ---------- 명중 직전 보정 ----------

    // 라이플 연속 명중 스택 (01-2 §8.5 스냅샷 예외). 첫 명중은 보너스 없이 스택 1, 이후 명중마다 (스택 x 비율)만큼 증가.
    // 다른 대상을 맞히거나 유지 시간이 지나면 초기화. 상한 없음.
    public void ModifyPlayerHit(ref DamageInfo info, ICombatTarget target)
    {
        if (info.Weapon != WeaponType.Rifle || rifleStackPercent <= 0f || target == null) return;
        float now = GameClock.CombatTime;
        if (!ReferenceEquals(target, rifleStackTarget) || now >= rifleStackUntil) rifleStacks = 0;
        if (rifleStacks > 0)
            info.Amount = PlayerStats.RoundHalfUp(info.Amount * (1f + rifleStacks * rifleStackPercent));
        rifleStackTarget = target;
        rifleStacks++;
        rifleStackUntil = now + GameBalance.Current.Artifact.RifleStackDuration;
    }

    // 권총 탄 발사 시 도탄(상쇄) 가능 여부를 한 번 정함
    public bool RollRicochet() => ricochetChance >= 1f || (ricochetChance > 0f && RandomStreams.Combat.NextDouble() < ricochetChance);
}
