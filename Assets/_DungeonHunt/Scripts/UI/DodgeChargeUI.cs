using UnityEngine;
using UnityEngine.UI;

public class DodgeChargeUI : MonoBehaviour
{
    [SerializeField] private PlayerMovement movement;
    [SerializeField] private Slider slider;
    [SerializeField] private GameObject root; // 켜고 끌 대상. 비워두면 이 오브젝트 자신을 끔

    private void Awake()
    {
        if (root == null) root = gameObject;
    }

    private void Update()
    {
        bool isFull = movement.DodgeCharge >= movement.DodgeMaxCharge;
        root.SetActive(!isFull);

        if (!isFull)
            slider.value = (float)movement.DodgeCharge / movement.DodgeMaxCharge;
    }
}
