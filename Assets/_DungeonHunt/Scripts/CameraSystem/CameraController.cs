using UnityEngine;

public enum CameraState
{
    Following,
    Transitioning,
}

// 02 카메라: 조준 오프셋 추적, 룸 경계 고정(복도는 제한 없음), 전투룸 진입 빠른 보간, 무방향 흔들림.
// 페이드 전환 자체는 UI의 FadeOverlay가 그리고, 여기선 상태와 즉시 이동만 처리함.
[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    public CameraState State { get; private set; } = CameraState.Following;
    public Camera Camera { get; private set; }

    private Rect? roomBounds;
    private Rect? catchupBounds;
    private bool catchingUp;
    private Vector2 logicalPosition;
    private Vector2 velocity;

    // 흔들림
    private float shakeAmplitude;
    private float shakeUntil;
    private float shakeDuration;
    private float lastShakeTime = -10f;
    private Vector2 shakeOffsetCurrent;

    public float ShakeStrength01 { get; set; } = 0.75f;

    private CameraBalance Balance => GameBalance.Current.Camera;

    private void Awake()
    {
        Camera = GetComponent<Camera>();
        Camera.orthographic = true;
        GameServices.Camera = this;
        ApplyZoom();
    }

    // 뷰포트 세로 = 캐릭터 키 6배 (02 §3.3, 1H = 1유닛)
    public void ApplyZoom()
    {
        if (Camera != null) Camera.orthographicSize = Balance.ViewportHeightInH * GameUnits.H * 0.5f;
    }

    public void SnapTo(Vector2 position)
    {
        logicalPosition = ClampToBounds(position, roomBounds);
        velocity = Vector2.zero;
        catchingUp = false;
        ApplyPosition(Vector2.zero);
    }

    public void SetTransitioning(bool transitioning)
    {
        State = transitioning ? CameraState.Transitioning : CameraState.Following;
        if (transitioning) GameEvents.Raise(GameEvents.CameraTransitionStarted);
        else GameEvents.Raise(GameEvents.CameraTransitionEnded);
    }

    // RoomEntered: 새 룸 경계. 일반 전투룸 첫 진입이면 직전 복도+연결부+새 룸 합집합 임시 경계로 빠른 보간 (02 §5.2)
    public void EnterRoom(RoomRuntime room, bool catchUp)
    {
        Rect bounds = room.CameraBounds;
        if (catchUp)
        {
            Rect current = CurrentViewRect();
            catchupBounds = Union(bounds, current);
            catchingUp = true;
        }
        else
        {
            catchingUp = false;
            catchupBounds = null;
        }
        roomBounds = bounds;
        GameEvents.Raise(GameEvents.CameraRoomBoundsChanged);
    }

    // 복도: 경계 제한 없이 조준 오프셋 추적
    public void EnterCorridor()
    {
        roomBounds = null;
        catchingUp = false;
        catchupBounds = null;
        GameEvents.Raise(GameEvents.CameraRoomBoundsChanged);
    }

    // 무방향 흔들림. 재발동 간격 안에서는 가장 강한 요청 하나만 (09 §5)
    public void Shake(float amplitude, float duration)
    {
        float now = Time.unscaledTime;
        float interval = GameBalance.Current.Feedback.HitShakeRetriggerInterval;
        bool active = now < shakeUntil;
        if (active && now - lastShakeTime < interval && amplitude <= shakeAmplitude) return;
        if (active && amplitude < shakeAmplitude) return;
        shakeAmplitude = amplitude;
        shakeDuration = Mathf.Max(0.01f, duration);
        shakeUntil = now + shakeDuration;
        lastShakeTime = now;
    }

    private void LateUpdate()
    {
        PlayerController player = GameServices.Player;
        if (player == null) return;
        if (State == CameraState.Transitioning)
        {
            ApplyPosition(Vector2.zero);
            return;
        }

        Vector2 target = (Vector2)player.transform.position + AimOffset(player);
        Rect? bounds = catchingUp ? catchupBounds : roomBounds;
        Vector2 clampedTarget = ClampToBounds(target, bounds);
        float smooth = catchingUp ? Balance.CombatEntryCatchupSmoothTime : Balance.FollowSmoothTime;
        logicalPosition = Vector2.SmoothDamp(logicalPosition, clampedTarget, ref velocity, smooth, Mathf.Infinity, Time.unscaledDeltaTime);

        if (catchingUp)
        {
            // 평상시 추적 목표(새 룸 경계 기준)와의 거리가 임계 이하면 복귀
            Vector2 normalTarget = ClampToBounds(target, roomBounds);
            if (Vector2.Distance(logicalPosition, normalTarget) <= Balance.CombatEntryCatchupExitDistance)
            {
                catchingUp = false;
                catchupBounds = null;
            }
        }

        ApplyPosition(ShakeOffset());
    }

    // 흔들림 전 화면 좌표 기준: ratio = clamp01(커서-플레이어 거리 / 화면 세로 절반) (02 §3.1)
    private Vector2 AimOffset(PlayerController player)
    {
        Vector2 cursor = player.Input.AimScreenPosition;
        Vector3 playerScreen3 = Camera.WorldToScreenPoint(new Vector3(player.transform.position.x, player.transform.position.y, 0f) + (Vector3)shakeOffsetCurrent);
        Vector2 playerScreen = playerScreen3;
        Vector2 delta = cursor - playerScreen;
        float d = delta.magnitude;
        if (d < 0.5f) return Vector2.zero;
        float ratio = Mathf.Clamp01(d / (Screen.height * 0.5f));
        return delta / d * Balance.AimOffsetDistance * ratio;
    }

    private Vector2 ShakeOffset()
    {
        float now = Time.unscaledTime;
        if (now >= shakeUntil || ShakeStrength01 <= 0f)
        {
            shakeOffsetCurrent = Vector2.zero;
            return Vector2.zero;
        }
        float t = (shakeUntil - now) / shakeDuration;
        float amp = shakeAmplitude * ShakeStrength01 * t;
        shakeOffsetCurrent = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * amp;
        return shakeOffsetCurrent;
    }

    private void ApplyPosition(Vector2 shake)
    {
        Vector3 p = new Vector3(logicalPosition.x + shake.x, logicalPosition.y + shake.y, transform.position.z == 0f ? -10f : transform.position.z);
        transform.position = p;
    }

    // 뷰포트보다 작은 축은 룸 중앙 고정 (02 §3.2)
    private Vector2 ClampToBounds(Vector2 target, Rect? bounds)
    {
        if (!bounds.HasValue || Camera == null) return target;
        Rect b = bounds.Value;
        float halfH = Camera.orthographicSize;
        float halfW = halfH * Camera.aspect;
        float x = b.width <= halfW * 2f ? b.center.x : Mathf.Clamp(target.x, b.xMin + halfW, b.xMax - halfW);
        float y = b.height <= halfH * 2f ? b.center.y : Mathf.Clamp(target.y, b.yMin + halfH, b.yMax - halfH);
        return new Vector2(x, y);
    }

    private Rect CurrentViewRect()
    {
        float halfH = Camera.orthographicSize;
        float halfW = halfH * Camera.aspect;
        return new Rect(logicalPosition.x - halfW, logicalPosition.y - halfH, halfW * 2f, halfH * 2f);
    }

    private static Rect Union(Rect a, Rect b)
    {
        float xMin = Mathf.Min(a.xMin, b.xMin), yMin = Mathf.Min(a.yMin, b.yMin);
        float xMax = Mathf.Max(a.xMax, b.xMax), yMax = Mathf.Max(a.yMax, b.yMax);
        return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
    }
}
