using UnityEngine;

// 03-6 FloorExitRoom: 전투/보상 없음. 이동 오브젝트와 F → 확인 없이 FloorExited.
public class FloorExitController : RoomContent
{
    protected override void OnBound()
    {
        RoomInteractable portal = RoomInteractable.Create("FloorExit", Runtime.transform, Runtime.GetPoint(RoomPointKind.RoomObject), PlaceholderArt.Ring, new Color(0.4f, 0.8f, 1f), 1.0f, "다음 층");
        portal.OnInteract = _ => GameServices.Run?.ExitFloor();
    }
}
