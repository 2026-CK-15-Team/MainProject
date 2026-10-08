using System;
using UnityEngine;

// 기획서 튜닝 파라미터 한곳 모음. 기본값은 전부 기획서의 PrototypeDefault (TODO_BALANCE) 그대로임.
// 값 바꿀 땐 이 에셋만 고치면 됨. 소유 문서 번호는 각 Header에 적음.
// 무기별 수치는 WeaponScriptable, 몬스터별 수치는 MonsterDefinition이 가짐.
[CreateAssetMenu(fileName = "GameBalance", menuName = "DungeonHunt/Game Balance")]
public class GameBalance : ScriptableObject
{
    public PlayerBalance Player = new PlayerBalance();
    public WeaponCommonBalance Weapon = new WeaponCommonBalance();
    public CameraBalance Camera = new CameraBalance();
    public CombatRoomBalance CombatRoom = new CombatRoomBalance();
    public ShopBalance Shop = new ShopBalance();
    public GambleBalance Gamble = new GambleBalance();
    public SecretRoomBalance SecretRoom = new SecretRoomBalance();
    public FloorEndBalance FloorEnd = new FloorEndBalance();
    public BossBalance Boss = new BossBalance();
    public ArtifactBalance Artifact = new ArtifactBalance();
    public MetaBalance Meta = new MetaBalance();
    public FeedbackBalance Feedback = new FeedbackBalance();
    public UIBalance UI = new UIBalance();

    // 에셋 없이 실행해도 기본값으로 돌아가게 함
    private static GameBalance fallback;
    public static GameBalance Current
    {
        get
        {
            if (GameServices.Balance != null) return GameServices.Balance;
            if (fallback == null) fallback = CreateInstance<GameBalance>();
            return fallback;
        }
    }
}

[Serializable]
public class PlayerBalance
{
    [Header("01-1 조작")]
    public float MoveMaxSpeed = 3.0f;          // H/s
    public float MoveAccelTime = 0.08f;
    public float MoveDecelTime = 0.08f;
    public float DodgeLength = 1.8f;           // H
    public float DodgeDuration = 0.32f;
    public float DodgeInvincibleDuration = 0.20f;
    public int DodgeMaxCharge = 2;
    public float DodgeChargeTime = 1.15f;
    public float JustDodgeWindow = 0.10f;
    public float InteractRange = 1.25f;        // H

    [Header("01-3 생존")]
    public float HitInvincibilityDuration = 0.55f;
    public float BodyRadius = 0.28f;           // 이동 충돌
    public float HurtboxRadius = 0.18f;        // 피격 판정

    [Header("01-4 스탯")]
    public int MaxHP = 6;
    [Range(0, 1)] public float CritChance = 0.05f;
    public float CritDamageMultiplier = 1.5f;
    public float CritChanceUpgradeStep = 0.02f;
    public float CritDamageUpgradeStep = 0.1f;
    public int StatUpgradeMaxLevel = 5;
    public float MoveSpeedFloor = 0.1f;        // 상태 배율 전 하한 (01-4 §7)
}

[Serializable]
public class WeaponCommonBalance
{
    [Header("01-2 무기 공통")]
    public float FireIntervalFloor = 0.04f;
    public float PistolMoveSpeedBonus = 0.10f;
    public int PistolDodgeChargeBonus = 1;
    public float RifleFireMoveSpeedMultiplier = 0.75f;
    public float ShotgunFireMoveSpeedMultiplier = 0.70f;
    public float ShotgunFireKickSlowDuration = 0.10f;
    [Range(0, 1)] public float JustDodgeAmmoRefundRatio = 0.25f;  // 최대 탄창 비율, 올림
    public float KnockbackDuration = 0.12f;
    public float CollisionTieEpsilon = 0.001f;
    // 라이플 "사격 중" 판정: 마지막 발사 뒤 이 시간 동안 사격 중으로 봄
    public float RifleFiringGrace = 0.12f;
}

[Serializable]
public class CameraBalance
{
    [Header("02 카메라")]
    public float ViewportHeightInH = 6f;       // 확정. 정사영 크기 = 3
    public float AimOffsetDistance = 0.65f;
    public float FollowSmoothTime = 0.12f;
    public float CombatEntryCatchupSmoothTime = 0.06f;
    public float CombatEntryCatchupExitDistance = 0.05f;
    public float TransitionFadeOut = 0.18f;
    public float TransitionFadeIn = 0.18f;
}

[Serializable]
public class CombatRoomBalance
{
    [Header("03-1 전투룸")]
    [Range(0, 1)] public float RewardArtifactChance = 0.20f;
    [Range(0, 1)] public float RewardCurrencyChance = 0.55f;
    [Range(0, 1)] public float RewardHPChance = 0.25f;
    public int RewardCurrencyAmount = 6;
    public int RewardHPAmount = 1;
    [Header("대형룸 전용 풀")]
    [Range(0, 1)] public float LargeArtifactChance = 0.40f;
    [Range(0, 1)] public float LargeCurrencyChance = 0.40f;
    [Range(0, 1)] public float LargeHPChance = 0.20f;
    public int LargeCurrencyAmount = 12;
    public int LargeHPAmount = 2;
    [Header("진행")]
    public float WaveTransitionWarningDuration = 1.0f;
    public float SpawnContactDamageGrace = 0.25f;
    public float RewardPickupEnableDelay = 0.25f;
    public int MonsterCurrencyDropDefault = 1;
    public float RoomRevealFadeDuration = 0.25f;
    public float FirstEntrySafeRadius = 2f;
}

[Serializable]
public class ShopBalance
{
    [Header("03-3 상점")]
    public Vector2Int SmallHealPrice = new Vector2Int(4, 6);
    public int SmallHealAmount = 1;
    public Vector2Int LargeHealPrice = new Vector2Int(7, 10);
    public int LargeHealAmount = 2;
    public int PermanentBundleAmount = 2;
    public Vector2Int PermanentBundlePrice = new Vector2Int(7, 10);
    [Range(0, 1)] public float FourSlotChance = 0.5f;     // 3개 또는 4개 각 50%
    [Range(0, 1)] public float ExtraSlotBundleChance = 0.5f;
    [Range(0, 1)] public float LargeHealChance = 0.5f;
    public Vector2Int CommonArtifactPrice = new Vector2Int(8, 11);
    public Vector2Int RareArtifactPrice = new Vector2Int(14, 18);
    public Vector2Int LegendaryArtifactPrice = new Vector2Int(24, 30);
    public int RerollCost = 6;
}

[Serializable]
public class GambleBalance
{
    [Header("03-4 도박룸")]
    public int HPCostPerAttempt = 1;
    [Range(0, 1)] public float BaseArtifactChance = 0.25f;
    [Range(0, 1)] public float PityIncrement = 0.20f;
}

[Serializable]
public class SecretRoomBalance
{
    [Header("03-5 비밀방 콘텐츠 가중치")]
    public float CurrencyWeight = 30f;
    public float HPWeight = 20f;
    public float ArtifactWeight = 35f;
    public float GambleWeight = 15f;
    [Tooltip("두 번째 UI 제작 폴백: 도박룸과 비밀방 도박 기능 제외")]
    public bool SecondUIFallback;
}

[Serializable]
public class FloorEndBalance
{
    [Header("03 / 03-6 층 진행")]
    public float WaitIndicatorDelay = 1f;
    public string[] FloorNames = { "1층 임시이름", "2층 임시이름", "3층 임시이름" };
}

[Serializable]
public class BossBalance
{
    [Header("05 보스 (더미)")]
    public int HP = 1200;
    [Range(0, 1)] public float PhaseTransitionThreshold = 0.5f;
    public float DeathMotionDuration = 1.5f;   // 개발 착수값
    public float IntroDuration = 1.5f;         // 등장 연출 길이 (개발 착수값)
    public int ContactDamage = 1;
    public float BodyRadius = 0.9f;
}

[Serializable]
public class ArtifactBalance
{
    [Header("07 아티팩트")]
    public int SlotCount = 6;
    public int Tier1Threshold = 2;
    public int Tier2Threshold = 4;
    public float CommonWeight = 65f;
    public float RareWeight = 27f;
    public float LegendaryWeight = 8f;
    [Range(0, 1)] public float SetBiasTriggerChance = 0.5f;
    public int RerollMaxCount = 2;
    public int RerollFreeCount = 1;
    public float FieldDropSearchRadius = 0.75f;
}

[Serializable]
public class MetaBalance
{
    [Header("06 런, 영구 진행")]
    public int PermanentPerRoomCleared = 2;
    public int[] FloorClearBonus = { 4, 7, 10 };
    public int ArtifactUnlockCost = 30;
    public int[] StatUpgradeCost = { 20, 35, 55, 80, 110 };
    public float RunEntryFadeOut = 0.18f;
    public float RunEntryFadeIn = 0.18f;
    public float DeathResultTransitionDuration = 0.75f;
    public float AbandonResultFadeDuration = 0.3f;
    public float EndingDuration = 12f;
    public string EndingLastLine = "이곳에 숨겨진 보물이 더 있을 것 같다";
}

[Serializable]
public class FeedbackBalance
{
    [Header("09 연출 (진폭H / 지속초)")]
    public Vector2 RifleHitShake = new Vector2(0.008f, 0.04f);
    public Vector2 PistolHitShake = new Vector2(0.018f, 0.06f);
    public Vector2 ShotgunHitShake = new Vector2(0.04f, 0.08f);
    public Vector2 PlayerHitShake = new Vector2(0.08f, 0.12f);
    public float HitShakeRetriggerInterval = 0.05f;
    public float MonsterWhiteFlashDuration = 0.06f;
    public float KillHitstopDuration = 0.04f;
    public float PlayerHitTintDuration = 0.06f;
    public float PlayerInvincibleBlinkPeriod = 0.08f;
    public float SetActivationToastDuration = 1.0f;
}

[Serializable]
public class UIBalance
{
    [Header("08 / 10 UI")]
    public float MonsterHPBarFadeDelay = 1.25f;
    public Vector3 FloorArrivalTextDuration = new Vector3(0.2f, 1.3f, 0.3f);
    public Vector2Int ReferenceResolution = new Vector2Int(1920, 1080);
    public float MinimapHUDViewRange = 20f;    // H. 1배 룸 하나와 출구가 들어오는 범위 (개발 착수값)
    [Range(0, 100)] public float ScreenShakeDefault = 75f;
}
