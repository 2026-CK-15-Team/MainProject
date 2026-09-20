using TMPro;
using UnityEngine;

public class FPSCounter : MonoBehaviour
{
    [SerializeField] private TMP_Text text;
    [SerializeField] private float updateInterval = 0.5f;

    private int frameCount;
    private float elapsed;

    private void Update()
    {
        frameCount++;
        elapsed += Time.unscaledDeltaTime;

        if (elapsed < updateInterval) return;

        float fps = frameCount / elapsed;
        text.text = $"{fps:F0} FPS";

        frameCount = 0;
        elapsed = 0f;
    }
}
