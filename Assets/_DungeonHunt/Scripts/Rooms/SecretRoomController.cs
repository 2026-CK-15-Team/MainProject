using UnityEngine;

public enum SecretContent
{
    Currency,
    Heal,
    Artifact,
    Gamble,
}

public enum SecretRoomState
{
    Hidden,
    Revealed,
}

// 03-5 비밀방. 콘텐츠는 런 계획이 미리 정함. 비밀 벽 파괴 순간 Revealed + 콘텐츠 활성화.
public class SecretRoomController : RoomContent
{
    public SecretRoomState State { get; private set; } = SecretRoomState.Hidden;
    public SecretContent ContentKind { get; set; }
    public GambleSession Gamble { get; private set; }

    public void Reveal()
    {
        if (State == SecretRoomState.Revealed) return;
        State = SecretRoomState.Revealed;
        GameEvents.Raise(GameEvents.SecretRoomRevealed, Runtime);

        CombatRoomBalance b = GameBalance.Current.CombatRoom;
        Vector2 reward = Runtime.GetPoint(RoomPointKind.RewardSpawn);
        switch (ContentKind)
        {
            case SecretContent.Currency:
                RewardPickup.Spawn(RewardKind.Currency, b.RewardCurrencyAmount, reward, Runtime);
                break;
            case SecretContent.Heal:
                RewardPickup.Spawn(RewardKind.Heal, b.RewardHPAmount, reward, Runtime);
                break;
            case SecretContent.Artifact:
                ArtifactDefinition def = GameServices.Artifacts?.DrawAndReserve(AcquisitionOrigin.SecretRoom);
                if (def != null) ArtifactPickup.Spawn(def, reward, AcquisitionOrigin.SecretRoom, true, Runtime);
                break;
            case SecretContent.Gamble:
                Gamble = new GambleSession(Runtime);
                GambleRoomController.CreateNpc(Runtime, Runtime.GetPoint(RoomPointKind.RoomObject), Gamble, null);
                break;
        }
    }
}
