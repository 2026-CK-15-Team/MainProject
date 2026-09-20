using UnityEngine;

public class ShopStockController : MonoBehaviour
{
    [SerializeField] private GameObject optionalFourthSlot; // 4슬롯일 때만 활성화되는 여분 HP회복 슬롯

    private void Awake()
    {
        bool useFourSlots = Random.value < 0.5f;
        if (optionalFourthSlot != null)
            optionalFourthSlot.SetActive(useFourSlots);
    }
}
