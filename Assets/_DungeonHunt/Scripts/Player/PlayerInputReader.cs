using System;
using UnityEngine;
using UnityEngine.InputSystem;

// 플레이어 게임 행동 입력. 키맵은 01-1 §3.
// - UI 입력(ESC/Tab/I)은 UIManager, 상호작용 F는 PlayerInteractor가 먼저 처리하고 이 프레임을 소비할 수 있음
// - 모달이 열려 있으면 게임 행동 입력 차단. 모달을 닫은 좌클릭은 버튼을 뗐다 다시 눌러야 공격됨 (08 §8.1)
// - 처치 히트스톱 중 새 입력은 종류별 첫 입력만 버퍼에 두었다가 끝나면 처리 (01-1 §7.3)
[DefaultExecutionOrder(-300)]
public class PlayerInputReader : MonoBehaviour, PlayerControls.IPlayerActions
{
    // 차단 중이면 0. 비정지 모달(아티팩트 획득, 상점 등)은 시간이 흐르므로 여기서 막아야 캐릭터가 안 걸어감
    public Vector2 MoveInput => Blocked ? Vector2.zero : rawMoveInput;
    public Vector2 AimScreenPosition { get; private set; }

    // 공격 유지 입력 (차단과 재입력 대기 반영)
    public bool IsAttackHeld => attackHeldRaw && !attackLatch && !Blocked;
    // 이번 프레임에 새로 누른 공격 (재장전 취소 판정용, 01-1 §7.2)
    public bool AttackPressedThisFrame { get; private set; }

    public event Action DodgePressed;
    public event Action<int> WeaponChangeRequested; // -1 = 이전 무기(Q), +1 = 다음 무기(E)
    public event Action ReloadPressed;

    private PlayerControls controls;
    private Vector2 rawMoveInput;
    private bool attackHeldRaw;
    private bool attackLatch;

    // 이번 프레임 들어온 입력
    private bool dodgeQueued;
    private int weaponQueued;
    private bool weaponConflict;
    private bool attackQueued;

    // 히트스톱 버퍼
    private bool bufferedDodge;
    private int bufferedWeapon;
    private bool bufferedReload;

    private bool Blocked => InputGate.GameplayBlocked || GameClock.IsPaused || !enabled;

    private void Awake()
    {
        controls = new PlayerControls();
        controls.Player.SetCallbacks(this);
    }

    private void OnEnable() => controls.Enable();
    private void OnDisable() => controls.Disable();
    private void OnDestroy() => controls.Dispose();

    public void OnMove(InputAction.CallbackContext context) => rawMoveInput = context.ReadValue<Vector2>();

    public void OnAimPosition(InputAction.CallbackContext context) => AimScreenPosition = context.ReadValue<Vector2>();

    public void OnAttack(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            attackHeldRaw = true;
            attackQueued = true;
        }
        if (context.canceled)
        {
            attackHeldRaw = false;
            attackLatch = false;
        }
    }

    public void OnDodge(InputAction.CallbackContext context)
    {
        if (context.performed) dodgeQueued = true;
    }

    public void OnWeaponChange(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        float value = context.ReadValue<float>();
        // Q와 E를 같은 틱에 누르면 둘 다 무시 (01-1 §7.2)
        if (Mathf.Approximately(value, 0f) || (weaponQueued != 0 && weaponQueued != Math.Sign(value)))
        {
            weaponConflict = true;
            return;
        }
        weaponQueued = Math.Sign(value);
    }

    // 모달이 열리면 호출. 지금 누르고 있는 공격은 뗄 때까지 무시.
    public void LatchAttack()
    {
        if (attackHeldRaw) attackLatch = true;
    }

    private void Update()
    {
        bool reloadQueued = InputGate.KeyDown(Key.R);
        int weapon = weaponConflict ? 0 : weaponQueued;
        AttackPressedThisFrame = false;

        if (Blocked || InputGate.ConsumedThisFrame)
        {
            if (Blocked) LatchAttack();
            ClearQueued();
            ClearBuffer();
            return;
        }

        if (GameClock.IsHitstop)
        {
            if (dodgeQueued) bufferedDodge = true;
            if (weapon != 0 && bufferedWeapon == 0) bufferedWeapon = weapon;
            if (reloadQueued) bufferedReload = true;
            ClearQueued();
            return;
        }

        bool doDodge = dodgeQueued || bufferedDodge;
        int doWeapon = weapon != 0 ? weapon : bufferedWeapon;
        bool doReload = reloadQueued || bufferedReload;
        ClearBuffer();

        // 회피 > 무기 전환 > 공격 > 재장전
        if (doDodge) DodgePressed?.Invoke();
        if (doWeapon != 0) WeaponChangeRequested?.Invoke(doWeapon);
        AttackPressedThisFrame = attackQueued && !attackLatch;
        if (doReload) ReloadPressed?.Invoke();
        ClearQueued();
    }

    private void ClearQueued()
    {
        dodgeQueued = false;
        weaponQueued = 0;
        weaponConflict = false;
        attackQueued = false;
    }

    private void ClearBuffer()
    {
        bufferedDodge = false;
        bufferedWeapon = 0;
        bufferedReload = false;
    }
}
