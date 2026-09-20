using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(DummyEnemy))]
public class MonsterHealthBar : MonoBehaviour
{
    private const float FadeDelay = 1.25f;

    [SerializeField] private GameObject barRoot; // 켜고 끌 대상. 이 스크립트가 붙은 오브젝트와 달라야 함
    [SerializeField] private Slider slider;

    private DummyEnemy enemy;
    private float hideTime;
    private bool visible;

    private void Awake()
    {
        enemy = GetComponent<DummyEnemy>();
    }

    private void OnEnable()
    {
        enemy.Damaged += OnDamaged;
        enemy.Died += OnDied;

        visible = false;
        if (barRoot != null) barRoot.SetActive(false);
    }

    private void OnDisable()
    {
        enemy.Damaged -= OnDamaged;
        enemy.Died -= OnDied;
    }

    private void Update()
    {
        if (!visible) return;

        UpdateFill();

        if (Time.time >= hideTime)
        {
            visible = false;
            if (barRoot != null) barRoot.SetActive(false);
        }
    }

    private void OnDamaged()
    {
        visible = true;
        hideTime = Time.time + FadeDelay;
        if (barRoot != null) barRoot.SetActive(true);
        UpdateFill();
    }

    private void OnDied(DummyEnemy _)
    {
        visible = false;
        if (barRoot != null) barRoot.SetActive(false);
    }

    private void UpdateFill()
    {
        if (slider != null)
            slider.value = (float)enemy.CurrentHP / enemy.MaxHP;
    }
}
