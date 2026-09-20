using UnityEngine;

public class TempDebugTimeScale : MonoBehaviour
{
    private float nextLogTime;

    private void Update()
    {
        if (Time.unscaledTime < nextLogTime) return;
        nextLogTime = Time.unscaledTime + 1f;

        Debug.Log($"[TempDebug] Time.timeScale={Time.timeScale}, HitStopState.IsActive={HitStopState.IsActive}, ModalGate.AnyModalOpen={ModalGate.AnyModalOpen}");
    }
}