using System;
using UnityEngine;

// 01-1 이동과 회피. 이동 상태(Idle/Move/Dodge)와 회피 충전 스택을 소유함.
// 수치는 PlayerStats 최종값을 읽음 (권총 패시브, 라이플/샷건 감속도 스탯 원천으로 들어옴).
[RequireComponent(typeof(PlayerInputReader))]
public class PlayerMovement : MonoBehaviour
{
    // ---- 상태 인스턴스 ----
    public readonly IdleState IdleState = new IdleState();
    public readonly MoveState MoveState = new MoveState();
    public readonly DodgeState DodgeState = new DodgeState();
    public IMovementState CurrentState { get; private set; }

    // ---- 런타임 값 ----
    public Vector2 CurrentVelocity { get; set; }
    public Vector2 ActualVelocity { get; private set; }   // 실제 이동량 / 시간 (선행 조준용)
    public int DodgeCharge { get; private set; }
    public float DodgeElapsed { get; set; }
    public bool WantsDodge { get; private set; }
    public Vector2 DodgeDirection { get; set; }
    public bool JustDodgeUsed { get; set; }

    public bool IsDodging => CurrentState == DodgeState;
    public bool IsDodgeInvincible => IsDodging && DodgeElapsed < Balance.DodgeInvincibleDuration;
    public bool IsInJustDodgeWindow => IsDodging && DodgeElapsed < stats.Get(StatId.JustDodgeWindow);

    public float MoveMaxSpeed => stats.Get(StatId.MoveSpeed);
    public int DodgeMaxCharge => stats.GetInt(StatId.DodgeMaxCharge);
    public float DodgeChargeTime => stats.Get(StatId.DodgeChargeTime);
    public float DodgeLength => Balance.DodgeLength;
    public float DodgeDuration => Balance.DodgeDuration;
    public float AccelSpeedPerSecond => MoveMaxSpeed / Mathf.Max(Balance.MoveAccelTime, 0.0001f);
    public float DecelSpeedPerSecond => MoveMaxSpeed / Mathf.Max(Balance.MoveDecelTime, 0.0001f);
    public float ChargeProgress01 => DodgeCharge >= DodgeMaxCharge ? 1f : Mathf.Clamp01(dodgeChargeTimer / DodgeChargeTime);

    public event Action DodgeStart;
    public event Action DodgeEnd;
    public event Action DodgeInvincibilityEnd;

    public PlayerInputReader Input { get; private set; }
    public PlayerBalance Balance => GameBalance.Current.Player;

    private PlayerStats stats;
    private float dodgeChargeTimer;

    private void Awake()
    {
        Input = GetComponent<PlayerInputReader>();
        stats = GetComponent<PlayerStats>();
        if (stats == null) stats = gameObject.AddComponent<PlayerStats>();

        // 이동은 직접 벽 검사로 처리 (물리 엔진에 맡기지 않음)
        if (TryGetComponent(out Rigidbody2D body))
        {
            body.bodyType = RigidbodyType2D.Kinematic;
        }
    }

    private void OnEnable()
    {
        Input.DodgePressed += OnDodgePressed;
        GameEvents.EffectiveStatChanged += OnStatChanged;
    }

    private void OnDisable()
    {
        Input.DodgePressed -= OnDodgePressed;
        GameEvents.EffectiveStatChanged -= OnStatChanged;
    }

    private void Start()
    {
        DodgeCharge = DodgeMaxCharge;
        ChangeState(IdleState);
    }

    private void Update()
    {
        float dt = GameClock.CombatDelta;
        if (dt <= 0f)
        {
            WantsDodge = false;
            return;
        }

        TickDodgeChargeRegen(dt);
        CurrentState.Tick(this, dt);
        WantsDodge = false;

        Vector2 before = transform.position;
        Vector2 moved = KinematicMover2D.Move(before, Balance.BodyRadius, CurrentVelocity * dt, out _);
        transform.position = before + moved;
        ActualVelocity = moved / dt;
    }

    public void ChangeState(IMovementState next)
    {
        CurrentState?.Exit(this);
        CurrentState = next;
        CurrentState.Enter(this);
    }

    public void ConsumeDodgeCharge()
    {
        DodgeCharge = Mathf.Max(0, DodgeCharge - 1);
    }

    public Vector2 GetAimDirection() =>
        AimUtility.ScreenPointToWorldDirection(Input.AimScreenPosition, transform.position);

    public void RaiseDodgeStart()
    {
        DodgeStart?.Invoke();
        GameEvents.Raise(GameEvents.DodgeStart);
    }

    public void RaiseDodgeEnd()
    {
        DodgeEnd?.Invoke();
        GameEvents.Raise(GameEvents.DodgeEnd);
    }

    public void RaiseDodgeInvincibilityEnd()
    {
        DodgeInvincibilityEnd?.Invoke();
        GameEvents.Raise(GameEvents.DodgeInvincibilityEnd);
    }

    // 저스트회피: 회피 1회당 최대 1회 (01-1 §4.1). 접촉 피해는 대상 아님 → 호출하는 쪽이 거름.
    public bool TryTriggerJustDodge()
    {
        if (!IsInJustDodgeWindow || JustDodgeUsed) return false;
        JustDodgeUsed = true;
        GameEvents.Raise(GameEvents.JustDodgeTriggered);
        return true;
    }

    // 층 전환 리프레시: 회피 충전 최대, 진행 중 충전 버림 (01-1 §4.4)
    public void RefillDodge()
    {
        DodgeCharge = DodgeMaxCharge;
        dodgeChargeTimer = 0f;
    }

    public void ResetForNewRun()
    {
        CurrentVelocity = Vector2.zero;
        ChangeState(IdleState);
        RefillDodge();
    }

    // 권총 장착/해제 때 WeaponController가 호출. 장착이면 현재 스택도 +보너스 (01-1 §8)
    public void OnPistolPassiveChanged(bool equipped, int bonus)
    {
        if (equipped) DodgeCharge += bonus;
        DodgeCharge = Mathf.Min(DodgeCharge, DodgeMaxCharge);
    }

    public void Teleport(Vector2 position)
    {
        transform.position = position;
        CurrentVelocity = Vector2.zero;
    }

    private void OnDodgePressed() => WantsDodge = true;

    private void OnStatChanged(StatId stat, float previous, float value)
    {
        if (stat == StatId.DodgeMaxCharge && DodgeCharge > DodgeMaxCharge)
            DodgeCharge = DodgeMaxCharge;
    }

    // 첫 스택을 쓰면 타이머 시작, 추가로 써도 진행률 유지, 최대면 정지 (01-1 §4.1 8번)
    private void TickDodgeChargeRegen(float deltaTime)
    {
        if (DodgeCharge >= DodgeMaxCharge)
        {
            dodgeChargeTimer = 0f;
            return;
        }

        dodgeChargeTimer += deltaTime;
        while (dodgeChargeTimer >= DodgeChargeTime && DodgeCharge < DodgeMaxCharge)
        {
            dodgeChargeTimer -= DodgeChargeTime;
            DodgeCharge++;
        }
        if (DodgeCharge >= DodgeMaxCharge) dodgeChargeTimer = 0f;
    }
}
