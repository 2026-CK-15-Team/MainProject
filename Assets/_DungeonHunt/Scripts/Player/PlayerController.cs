using UnityEngine;

// 플레이어 엔티티 묶음. 하위 시스템 참조와 런/층 단위 초기화 진입점.
// 상태 소유: 이동 01-1(PlayerMovement), 무기 01-2(WeaponController), 생존 01-3(PlayerHealth), 스탯 01-4(PlayerStats)
[DefaultExecutionOrder(-400)]
public class PlayerController : MonoBehaviour
{
    public PlayerInputReader Input { get; private set; }
    public PlayerMovement Movement { get; private set; }
    public PlayerHealth Health { get; private set; }
    public PlayerStats Stats { get; private set; }
    public WeaponController Weapons { get; private set; }
    public PlayerInteractor Interactor { get; private set; }
    public PlayerVisual Visual { get; private set; }

    public Vector2 Position => transform.position;
    public float BodyRadius => GameBalance.Current.Player.BodyRadius;
    public float HurtboxRadius => GameBalance.Current.Player.HurtboxRadius;

    private void Awake()
    {
        Stats = GetOrAdd<PlayerStats>();
        Input = GetOrAdd<PlayerInputReader>();
        Movement = GetOrAdd<PlayerMovement>();
        Health = GetOrAdd<PlayerHealth>();
        Visual = GetOrAdd<PlayerVisual>();
        Interactor = GetOrAdd<PlayerInteractor>();
        Weapons = GetOrAdd<WeaponController>();
        GameServices.Player = this;
    }

    private void OnDestroy()
    {
        if (GameServices.Player == this) GameServices.Player = null;
    }

    // 런 시작: HP 풀피, 탄창·회피 최대, 라이플 장착, 런 한정 수정값 제거 (06 §5.1 2번)
    public void ResetForNewRun()
    {
        Stats.ClearRunSources();
        Health.ResetForNewRun();
        Movement.ResetForNewRun();
        Weapons.ResetForNewRun();
        enabled = true;
    }

    // 층 전환 리프레시: 회피 충전, 탄창, 재장전 취소. HP 유지 (01-1 §4.4)
    public void RefreshForNewFloor()
    {
        Movement.RefillDodge();
        Weapons.RefillAllForNewFloor();
    }

    public void PlaceAt(Vector2 position)
    {
        Movement.Teleport(position);
    }

    private T GetOrAdd<T>() where T : Component
    {
        T component = GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }
}
