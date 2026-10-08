using UnityEngine;

// 03-2 보물룸: 최초 진입 시 필드 아티팩트 1개 확정 생성 (RoomObjectPoint). 재방문 시 추가 생성 없음.
public class TreasureRoomController : RoomContent
{
    private bool spawned;

    public override void OnPlayerEntered(bool first)
    {
        if (spawned) return;
        spawned = true;
        ArtifactDefinition def = GameServices.Artifacts?.DrawAndReserve(AcquisitionOrigin.TreasureRoom);
        // 유효 후보 0개면 생성 취소, 다른 보상으로 대체 안 함 (03-2 §7)
        if (def == null) return;
        ArtifactPickup.Spawn(def, Runtime.GetPoint(RoomPointKind.RoomObject), AcquisitionOrigin.TreasureRoom, true, Runtime);
    }
}
