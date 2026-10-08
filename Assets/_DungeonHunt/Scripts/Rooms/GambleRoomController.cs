using UnityEngine;

// 03-4 도박룸: RoomObjectPoint에 도박 NPC. 입장/대화 비용 없음.
public class GambleRoomController : RoomContent
{
    public GambleSession Session { get; private set; }
    public bool UIOpen { get; set; }
    public override bool BlocksModal => UIOpen;

    protected override void OnBound()
    {
        Session = new GambleSession(Runtime);
        CreateNpc(Runtime, Runtime.GetPoint(RoomPointKind.RoomObject), Session, this);
    }

    public static RoomInteractable CreateNpc(RoomRuntime room, Vector2 at, GambleSession session, GambleRoomController owner)
    {
        RoomInteractable npc = RoomInteractable.Create("GambleNPC", room.transform, at, PlaceholderArt.Circle, new Color(0.75f, 0.35f, 0.75f), 0.8f, "도박");
        npc.OnInteract = _ =>
        {
            GameEvents.Raise(GameEvents.GambleRoomUIOpened, room);
            GameServices.UI?.OpenGamble(session, owner);
        };
        return npc;
    }
}
