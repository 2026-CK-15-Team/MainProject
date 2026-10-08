using UnityEngine;

// 03-6 RunEndTreasureRoom: 일반 보물상자 + 외형이 다른 전용 엔딩 상자.
// 엔딩 상자를 여는 순간 EndingTriggered. 엔딩 상자는 인런 보상을 지급하지 않음.
// 일반 보물상자 내용물은 기획 미정 → 열림 표시만 함.
public class RunEndTreasureController : RoomContent
{
    private bool normalOpened;

    protected override void OnBound()
    {
        RoomInteractable normal = RoomInteractable.Create("NormalChest", Runtime.transform, Runtime.GetPoint(RoomPointKind.RoomObject, RoomObjectRole.NormalChest), PlaceholderArt.Square, new Color(0.65f, 0.45f, 0.2f), 0.7f, "보물상자");
        normal.CanInteractCheck = () => !normalOpened;
        normal.OnInteract = _ =>
        {
            normalOpened = true;
            normal.SetColor(new Color(0.35f, 0.25f, 0.12f));
            GameServices.UI?.ShowCenterToast("상자가 비어 있다 (내용 미정)", 1.2f);
        };

        RoomInteractable ending = RoomInteractable.Create("EndingChest", Runtime.transform, Runtime.GetPoint(RoomPointKind.RoomObject, RoomObjectRole.EndingChest), PlaceholderArt.Square, new Color(0.95f, 0.85f, 0.3f), 1.0f, "엔딩 상자");
        ending.OnInteract = _ => GameServices.Run?.TriggerEnding();
    }
}
