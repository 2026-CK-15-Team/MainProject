using UnityEngine;

public enum GambleState
{
    Active,
    Depleted,
}

// 03-4 도박 한 자리 (도박룸 또는 비밀방 도박 기능). 런 재화를 걸고 천장형 확률로 아티팩트 1개.
public class GambleSession
{
    public GambleState State { get; private set; } = GambleState.Active;
    public int Failures { get; private set; }
    public RoomRuntime Room { get; }

    public GambleSession(RoomRuntime room) { Room = room; }

    private GambleBalance B => GameBalance.Current.Gamble;

    public float CurrentChance => Mathf.Min(1f, B.BaseArtifactChance + B.PityIncrement * Failures);
    public int Cost => B.CurrencyCostPerAttempt;

    // 시도 가능 조건: 런 재화 >= 시도 비용
    public bool CanAttempt => State == GambleState.Active && GameServices.Wallet != null && GameServices.Wallet.CanAfford(Cost);

    public enum AttemptResult { NotAllowed, Lost, Won, ReservationFailed }

    // 당첨했지만 아직 지급 안 한 아티팩트 (결과 카드 공개 연출 중). Deliver로 지급
    public ArtifactDefinition PendingDelivery { get; private set; }

    public void Deliver()
    {
        ArtifactDefinition def = PendingDelivery;
        PendingDelivery = null;
        if (def != null) GameServices.Artifacts.GrantImmediate(def, AcquisitionOrigin.Gamble);
    }

    // 당첨이면 후보 예약 성공 뒤에 재화 소모, 미당첨이면 바로 재화 소모 (03-4 §3)
    // deferDelivery면 당첨 아티팩트를 PendingDelivery에 두고 지급은 Deliver에서 (결과 카드 공개 뒤)
    public AttemptResult Attempt(out ArtifactDefinition won, bool deferDelivery = false)
    {
        won = null;
        if (!CanAttempt) return AttemptResult.NotAllowed;
        bool win = RandomStreams.Loot.NextDouble() < CurrentChance;

        if (!win)
        {
            GameServices.Wallet.TrySpend(Cost);
            Failures++;
            GameEvents.Raise(GameEvents.GambleRoomAttempted, Room);
            return AttemptResult.Lost;
        }

        won = GameServices.Artifacts?.DrawAndReserve(AcquisitionOrigin.Gamble);
        if (won == null)
        {
            Debug.LogError("도박: 당첨 후보 예약 실패. 재화/천장/상태 유지 (03-4 §7)");
            return AttemptResult.ReservationFailed;
        }
        GameServices.Wallet.TrySpend(Cost);
        State = GambleState.Depleted;
        GameEvents.Raise(GameEvents.GambleRoomAttempted, Room);
        GameEvents.Raise(GameEvents.GambleRoomArtifactWon, Room);
        PendingDelivery = won;
        if (!deferDelivery) Deliver();
        return AttemptResult.Won;
    }
}
