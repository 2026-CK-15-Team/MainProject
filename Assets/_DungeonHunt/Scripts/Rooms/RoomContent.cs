using UnityEngine;

// 룸 종류별 내부 규칙의 공통 틀 (03-1 ~ 03-6). RoomRuntime 옆에 붙음.
public abstract class RoomContent : MonoBehaviour
{
    public RoomRuntime Runtime { get; private set; }

    // 잠긴 전투 상태 (문 잠금, 모달 차단, 순간이동 차단)
    public virtual bool IsLocked => false;
    // 잠금은 아니지만 모달을 막는 상태 (도박 상호작용 중 등)
    public virtual bool BlocksModal => false;

    public void Bind(RoomRuntime runtime)
    {
        Runtime = runtime;
        OnBound();
    }

    protected virtual void OnBound() { }

    public virtual void OnPlayerEntered(bool first) { }
}
