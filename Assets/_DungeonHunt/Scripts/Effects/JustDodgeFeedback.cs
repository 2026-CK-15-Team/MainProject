using System.Collections;
using UnityEngine;

public class JustDodgeFeedback : MonoBehaviour
{
    [SerializeField] private PlayerMovement movement;
    [SerializeField] private GameObject outlineObject; // 확대된 흰색 스프라이트, 평소엔 꺼둔 상태
    [SerializeField] private float duration = 0.2f;

    private Coroutine activeCoroutine;

    private void OnEnable() => movement.JustDodgeTriggered += OnJustDodge;
    private void OnDisable() => movement.JustDodgeTriggered -= OnJustDodge;

    private void OnJustDodge()
    {
        if (activeCoroutine != null) StopCoroutine(activeCoroutine);
        activeCoroutine = StartCoroutine(ShowOutline());
    }

    private IEnumerator ShowOutline()
    {
        outlineObject.SetActive(true);
        yield return new WaitForSeconds(duration);
        outlineObject.SetActive(false);
        activeCoroutine = null;
    }
}
