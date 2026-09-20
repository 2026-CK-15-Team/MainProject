using Unity.Cinemachine;
using UnityEngine;

public class ScreenShake : MonoBehaviour
{
    public static ScreenShake Instance { get; private set; }

    [SerializeField] private CinemachineImpulseSource impulseSource;

    private const float RetriggerInterval = 0.05f;

    private float lastShakeRealTime = float.NegativeInfinity;
    private float lastShakeAmplitude;

    private void Awake() => Instance = this;

    /// <summary>재발동 간격(0.05s) 안에서는 가장 강한 요청 하나만 반영한다.</summary>
    public void Shake(float amplitude)
    {
        bool withinRetriggerWindow = Time.unscaledTime - lastShakeRealTime < RetriggerInterval;
        if (withinRetriggerWindow && amplitude <= lastShakeAmplitude) return;

        lastShakeRealTime = Time.unscaledTime;
        lastShakeAmplitude = amplitude;
        impulseSource.GenerateImpulse(amplitude);
    }
}
