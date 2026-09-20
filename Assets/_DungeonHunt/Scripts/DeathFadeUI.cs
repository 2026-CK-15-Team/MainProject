using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class DeathFadeUI : MonoBehaviour
{
    [SerializeField] private Image fadeImage;
    [SerializeField] private float duration = 0.75f;

    public void PlayThen(Action onComplete)
    {
        StartCoroutine(FadeRoutine(onComplete));
    }

    private IEnumerator FadeRoutine(Action onComplete)
    {
        float elapsed = 0f;
        Color c = fadeImage.color;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            c.a = Mathf.Clamp01(elapsed / duration);
            fadeImage.color = c;
            yield return null;
        }

        c.a = 1f;
        fadeImage.color = c;
        onComplete?.Invoke();
    }
}
