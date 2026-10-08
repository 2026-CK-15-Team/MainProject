using System;

// 기획서 이벤트 이름 그대로 모은 전역 이벤트 버스. 발행은 각 소유 시스템만 함.
// 런/씬 전환 때 구독 해제는 구독한 쪽 책임 (OnDisable).
public static class GameEvents
{
    // 01-1 조작
    public static Action DodgeStart, DodgeEnd, DodgeInvincibilityEnd, JustDodgeTriggered;
    public static Action<WeaponType> WeaponSwapped, WeaponSwapReady, ReloadStart, ReloadComplete;

    // 01-2 무기
    public static Action<WeaponType> WeaponFired;
    public static Action<DamageInfo> ProjectileHit, ShotgunConeHit, CriticalHit;
    public static Action<int> JustDodgeAmmoRefund;

    // 01-3 생존 / 01-4 스탯
    public static Action<int> PlayerHit;              // 받은 피해
    public static Action<int, int> PlayerHPChanged;   // 현재, 최대
    public static Action PlayerDied;
    public static Action<StatId, float, float> EffectiveStatChanged;

    // 03 던전
    public static Action<int> FloorGenerated, FloorExited, FloorReached;
    public static Action<RoomRuntime> RoomEntered;
    public static Action CorridorEntered, FloorExitAvailable, EndingTriggered;

    // 03-1 ~ 03-6 룸
    public static Action<RoomRuntime> CombatRoomLocked, CombatRoomCleared;
    public static Action<RoomRuntime> FloorGateRoomLocked, FloorGateRoomCleared, EliteGateWaveAdvanced, EliteGateRewardSpawned;
    public static Action<RoomRuntime> ShopStockGenerated, ShopItemPurchased;
    public static Action<RoomRuntime> GambleRoomUIOpened, GambleRoomAttempted, GambleRoomArtifactWon;
    public static Action WallAttacked;
    public static Action<RoomRuntime> SecretRoomRevealed;
    public static Action<int> RunCurrencyGained, RunCurrencyLost;

    // 04 몬스터 / 05 보스
    public static Action<Monster> MonsterSpawned, MonsterAttackTelegraphed, MonsterAttackExecuted, MonsterContactDamage, MonsterDied;
    public static Action BossEncounterStarted, BossPhaseChanged, BossDefeated;

    // 06 런, 저장
    public static Action SaveLoaded, RunStarted, RunCleared, RunOver, GameSaved;
    public static Action<int> PermanentCurrencyGranted;
    public static Action<string> ArtifactUnlockedViaCurrency, ArtifactProgressUnlockPending, ArtifactProgressUnlockRevealed;
    public static Action<StatId, int> BaseStatUpgraded;

    // 07 아티팩트
    public static Action<ArtifactDefinition> ArtifactPickedUp, ArtifactAcquired, ArtifactNotSelected;
    public static Action<ArtifactDefinition> ArtifactEquipped, ArtifactUnequipped, ArtifactDroppedToField, ArtifactRerolled;
    public static Action<ArtifactSetDefinition, SetTier> SetTierChanged;

    // 02 카메라
    public static Action CameraRoomBoundsChanged, CameraTransitionStarted, CameraTransitionEnded;

    public static void Raise(Action action) => action?.Invoke();
    public static void Raise<T>(Action<T> action, T value) => action?.Invoke(value);
    public static void Raise<T1, T2>(Action<T1, T2> action, T1 a, T2 b) => action?.Invoke(a, b);
}
