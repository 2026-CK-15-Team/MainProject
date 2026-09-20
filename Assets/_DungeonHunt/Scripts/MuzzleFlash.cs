using System.Collections;
using UnityEngine;

public class MuzzleFlash : MonoBehaviour
{
    [SerializeField] private GameObject visual;
    [SerializeField] private float duration = 0.05f;

    public void Show()
    {
        StopAllCoroutines();
        StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        visual.SetActive(true);
        yield return new WaitForSecondsRealtime(duration);
        visual.SetActive(false);
    }
}
