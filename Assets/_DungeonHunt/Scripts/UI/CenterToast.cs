using System.Collections;
using TMPro;
using UnityEngine;

public class CenterToast : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text text;

    private Coroutine activeCoroutine;

    public void Show(string message, float duration)
    {
        if (activeCoroutine != null) StopCoroutine(activeCoroutine);
        activeCoroutine = StartCoroutine(ShowRoutine(message, duration));
    }

    private IEnumerator ShowRoutine(string message, float duration)
    {
        text.text = message;
        root.SetActive(true);
        yield return new WaitForSecondsRealtime(duration);
        root.SetActive(false);
        activeCoroutine = null;
    }
}
