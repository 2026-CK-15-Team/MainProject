using TMPro;
using UnityEngine;

public class PlaytestLogFailureToast : MonoBehaviour
{
    [SerializeField] private GameObject root; // 평소엔 꺼둔 상태. 이 스크립트가 붙은 오브젝트와 달라야 함
    [SerializeField] private TMP_Text text;
    private const float Duration = 3f;

    private float hideTime;
    private bool visible;

    public void Show()
    {
        text.text = "Log save failed";
        hideTime = Time.unscaledTime + Duration;

        if (!visible)
        {
            visible = true;
            root.SetActive(true);
        }
    }

    private void Update()
    {
        if (!visible) return;

        if (Time.unscaledTime >= hideTime)
        {
            visible = false;
            root.SetActive(false);
        }
    }
}
