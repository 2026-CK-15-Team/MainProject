using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerInputReader))]
public class PlayerMovement : MonoBehaviour
{
    [Header("이동 파라미터")]
    public float MoveMaxSpeed = 6f;
    public float MoveAccelTime = 0.08f;
    public float MoveDecelTime = 0.08f;

    [Header("회피 파라미터")]
    public float DodgeLength = 3f;
    public float DodgeDuration = 0.25f;
    public float DodgeInvincibleDuration = 0.15f;
    public int DodgeMaxCharge = 2;
    public float DodgeChargeTime = 2f;

    // ---- 상태 인스턴스 ----
    public readonly IdleState IdleState = new IdleState();
    public readonly MoveState MoveState = new MoveState();
    public readonly DodgeState DodgeState = new DodgeState();
    public IMovementState CurrentState { get; private set; }

    // ---- 런타임 값 ----
    public Vector2 CurrentVelocity { get; set; }
    public int DodgeCharge { get; private set; }
    public float DodgeElapsed { get; set; }
    public bool WantsDodge { get; private set; }

    public bool IsDodgeInvincible =>
        CurrentState == DodgeState && DodgeElapsed < DodgeInvincibleDuration;

    public float AccelSpeedPerSecond => MoveMaxSpeed / Mathf.Max(MoveAccelTime, 0.0001f);
    public float DecelSpeedPerSecond => MoveMaxSpeed / Mathf.Max(MoveDecelTime, 0.0001f);

    public event Action DodgeStart;
    public event Action DodgeEnd;
    public event Action DodgeInvincibilityEnd;

    public PlayerInputReader Input { get; private set; }

    private Rigidbody2D rb;
    private float dodgeChargeTimer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        Input = GetComponent<PlayerInputReader>();
        DodgeCharge = DodgeMaxCharge;
    }

    private void OnEnable() => Input.DodgePressed += OnDodgePressed;
    private void OnDisable() => Input.DodgePressed -= OnDodgePressed;

    private void Start() => ChangeState(IdleState);

    private void FixedUpdate()
    {
        TickDodgeChargeRegen(Time.fixedDeltaTime);

        CurrentState.Tick(this, Time.fixedDeltaTime);
        WantsDodge = false;

        rb.MovePosition(rb.position + CurrentVelocity * Time.fixedDeltaTime);
    }

    public void ChangeState(IMovementState next)
    {
        CurrentState?.Exit(this);
        CurrentState = next;
        CurrentState.Enter(this);
    }

    public void ConsumeDodgeCharge() => DodgeCharge = Mathf.Max(0, DodgeCharge - 1);

    public Vector2 GetAimDirection() =>
        AimUtility.ScreenPointToWorldDirection(Input.AimScreenPosition, transform.position);

    public void RaiseDodgeStart() => DodgeStart?.Invoke();
    public void RaiseDodgeEnd() => DodgeEnd?.Invoke();
    public void RaiseDodgeInvincibilityEnd() => DodgeInvincibilityEnd?.Invoke();

    private void OnDodgePressed() => WantsDodge = true;

    private void TickDodgeChargeRegen(float deltaTime)
    {
        if (DodgeCharge >= DodgeMaxCharge)
        {
            dodgeChargeTimer = 0f;
            return;
        }

        dodgeChargeTimer += deltaTime;
        if (dodgeChargeTimer >= DodgeChargeTime)
        {
            dodgeChargeTimer = 0f;
            DodgeCharge++;
        }
    }
}