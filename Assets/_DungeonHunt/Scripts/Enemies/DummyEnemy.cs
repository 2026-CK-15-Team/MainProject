using System;
using UnityEngine;

// 테스트용 과녁. 공격하지 않고 접촉 피해만 줌. 실제 몬스터는 Monster + MonsterDefinition 사용.
public class DummyEnemy : MonoBehaviour, ICombatTarget
{
    [Header("테스트용 값 (기획 확정값 아님)")]
    public int MaxHP = 30;
    public int ContactDamage = 1;
    public float Radius = 0.35f;

    public int CurrentHP { get; private set; }
    public event Action<DummyEnemy> Died;

    public Vector2 Position => transform.position;
    public float HitRadius => Radius;
    public bool CanBeHit => CurrentHP > 0;
    public int SpawnOrder => 0;

    private void Awake() => CurrentHP = MaxHP;
    private void OnEnable() => CombatTargets.Register(this);
    private void OnDisable() => CombatTargets.Unregister(this);

    public void TakeDamage(DamageInfo info)
    {
        if (CurrentHP <= 0) return;
        CurrentHP -= info.Amount;
        GameServices.Feedback?.OnMonsterHit(info, Position);
        if (CurrentHP <= 0)
        {
            Died?.Invoke(this);
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        PlayerController player = GameServices.Player;
        if (player == null || GameClock.CombatDelta <= 0f) return;
        if (Vector2.Distance(Position, player.Position) <= Radius + player.HurtboxRadius)
            player.Health.ReceiveAttack(new IncomingAttack { Owner = this, Damage = ContactDamage, IsContact = true });
    }
}
