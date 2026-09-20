using TMPro;
using UnityEngine;

public class DamageNumber : MonoBehaviour
{
    [SerializeField] private TMP_Text text;
    [SerializeField] private float lifetime = 0.6f;
    [SerializeField] private float riseSpeed = 0.5f;
    [SerializeField] private float normalFontSize = 3f;
    [SerializeField] private float criticalFontSize = 5f;

    public void Show(float amount, bool isCritical)
    {
        text.text = Mathf.RoundToInt(amount).ToString();
        text.color = isCritical ? Color.yellow : Color.white;
        text.fontSize = isCritical ? criticalFontSize : normalFontSize;

        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        transform.position += Vector3.up * riseSpeed * Time.unscaledDeltaTime;
    }
}
