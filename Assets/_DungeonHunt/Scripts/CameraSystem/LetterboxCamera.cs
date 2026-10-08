using UnityEngine;

// 08 §7 AspectRatio_OutsideHandling: 게임 뷰는 16:9 유지, 남는 영역은 필러박스/레터박스(검은 띠).
// 메인 카메라 viewport rect를 16:9로 맞추고, 띠 영역은 뒤쪽 배경 카메라가 검게 지움.
// UI는 UIManager가 같은 16:9 영역 안에 배치함.
[RequireComponent(typeof(Camera))]
public class LetterboxCamera : MonoBehaviour
{
    public const float TargetAspect = 16f / 9f;

    private Camera cam;
    private Camera background;
    private int lastWidth = -1, lastHeight = -1;

    // 현재 화면에서 16:9 게임 뷰가 차지하는 정규화 영역
    public static Rect ViewportRect(int width, int height)
    {
        if (width <= 0 || height <= 0) return new Rect(0f, 0f, 1f, 1f);
        float screenAspect = (float)width / height;
        if (screenAspect > TargetAspect)
        {
            float w = TargetAspect / screenAspect;      // 필러박스 (좌우 띠)
            return new Rect((1f - w) * 0.5f, 0f, w, 1f);
        }
        float h = screenAspect / TargetAspect;          // 레터박스 (위아래 띠)
        return new Rect(0f, (1f - h) * 0.5f, 1f, h);
    }

    private void Awake()
    {
        cam = GetComponent<Camera>();
        var go = new GameObject("LetterboxBackground");
        go.transform.SetParent(transform, false);
        background = go.AddComponent<Camera>();
        background.depth = cam.depth - 10f;
        background.clearFlags = CameraClearFlags.SolidColor;
        background.backgroundColor = Color.black;
        background.cullingMask = 0;
        background.orthographic = true;
    }

    private void LateUpdate()
    {
        if (Screen.width == lastWidth && Screen.height == lastHeight) return;
        lastWidth = Screen.width;
        lastHeight = Screen.height;
        cam.rect = ViewportRect(lastWidth, lastHeight);
    }
}
