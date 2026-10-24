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
    [Tooltip("최대 이동속도 (H/초). 이동속도 스탯의 기본값")]
    public float MoveMaxSpeed = 3.0f;
    [Tooltip("정지 → 최대 속도까지 걸리는 시간 (초)")]
    public float MoveAccelTime = 0.08f;
    [Tooltip("최대 속도 → 정지까지 걸리는 시간 (초)")]
    public float MoveDecelTime = 0.08f;
    [Tooltip("회피 1회 이동 거리 (H)")]
    public float DodgeLength = 1.8f;
    [Tooltip("회피 동작 전체 길이 (초)")]
    public float DodgeDuration = 0.32f;
    [Tooltip("회피 시작부터 무적인 시간 (초)")]
    public float DodgeInvincibleDuration = 0.20f;
    [Tooltip("회피 최대 충전 스택 수")]
    public int DodgeMaxCharge = 2;
    [Tooltip("회피 스택 1개 충전 시간 (초)")]
    public float DodgeChargeTime = 1.15f;
    [Tooltip("회피 시작 후 이 시간 안에 공격을 받으면 저스트회피 (초)")]
    public float JustDodgeWindow = 0.10f;
    [Tooltip("상호작용 가능 거리 (H)")]
    public float InteractRange = 1.25f;

    [Header("01-3 생존")]
    [Tooltip("피격 후 무적 시간 (초)")]
    public float HitInvincibilityDuration = 0.55f;
    [Tooltip("이동 충돌 반경 (H). 벽 충돌용")]
    public float BodyRadius = 0.28f;
    [Tooltip("피격 판정 반경 (H). 이동 충돌 반경보다 작게 둠")]
    public float HurtboxRadius = 0.18f;

    [Header("01-4 스탯")]
    [Tooltip("시작 최대 HP. 몬스터 피해와 회복량은 이 규모(약 50)에 맞춘 값")]
    public int MaxHP = 50;
    [Tooltip("기본 치명타 확률 (0~1)")]
    [Range(0, 1)] public float CritChance = 0.05f;
    [Tooltip("치명타 피해 배율")]
    public float CritDamageMultiplier = 1.5f;
    [Tooltip("이동속도 하한 (H/초). 비율 수정값 뒤, 상태 배율 앞에 적용 (01-4 §7)")]
    public float MoveSpeedFloor = 0.1f;
}

[Serializable]
public class WeaponCommonBalance
{
    [Header("01-2 무기 공통")]
    [Tooltip("모든 무기 발사 간격 하한 (초)")]
    public float FireIntervalFloor = 0.04f;
    [Tooltip("권총 들고 있을 때 이동속도 비율 보너스 (0.1 = +10%)")]
    public float PistolMoveSpeedBonus = 0.10f;
    [Tooltip("권총 들고 있을 때 회피 최대 충전 추가 수")]
    public int PistolDodgeChargeBonus = 1;
    [Tooltip("라이플 사격 중 이동속도 배율")]
    public float RifleFireMoveSpeedMultiplier = 0.75f;
    [Tooltip("샷건 발사 직후 이동속도 배율")]
    public float ShotgunFireMoveSpeedMultiplier = 0.70f;
    [Tooltip("샷건 발사 후 감속 유지 시간 (초)")]
    public float ShotgunFireKickSlowDuration = 0.10f;
    [Tooltip("저스트회피 시 탄 회복량. 최대 탄창 대비 비율, 올림")]
    [Range(0, 1)] public float JustDodgeAmmoRefundRatio = 0.25f;
    [Tooltip("넉백 지속 시간 (초)")]
    public float KnockbackDuration = 0.12f;
    [Tooltip("투사체가 벽과 몬스터에 거의 동시에 닿을 때 같은 거리로 보는 오차 (H)")]
    public float CollisionTieEpsilon = 0.001f;
    [Tooltip("마지막 발사 후 이 시간 동안 라이플을 '사격 중'으로 봄 (초)")]
    public float RifleFiringGrace = 0.12f;
}

[Serializable]
public class CameraBalance
{
    [Header("02 카메라")]
    [Tooltip("화면 세로에 보이는 높이 (H). 확정값. 정사영 크기 = 이 값 / 2")]
    public float ViewportHeightInH = 6f;
    [Tooltip("조준 방향으로 카메라를 미는 최대 거리 (H)")]
    public float AimOffsetDistance = 0.65f;
    [Tooltip("평상시 카메라 추적 지연 (SmoothDamp 시간, 초)")]
    public float FollowSmoothTime = 0.12f;
    [Tooltip("전투룸 진입 직후 룸 경계 안으로 따라잡을 때 추적 지연 (초)")]
    public float CombatEntryCatchupSmoothTime = 0.06f;
    [Tooltip("목표와 이 거리 안이 되면 따라잡기 종료 (H)")]
    public float CombatEntryCatchupExitDistance = 0.05f;
    [Tooltip("룸 이동 시 페이드 아웃 시간 (초)")]
    public float TransitionFadeOut = 0.18f;
    [Tooltip("룸 이동 시 페이드 인 시간 (초)")]
    public float TransitionFadeIn = 0.18f;
}

[Serializable]
public class CombatRoomBalance
{
    [Header("03-1 전투룸")]
    [Tooltip("클리어 보상 가중치: 아티팩트. 세 값의 합으로 나눠 1회 추첨")]
    [Range(0, 1)] public float RewardArtifactChance = 0.20f;
    [Tooltip("클리어 보상 가중치: 런 재화")]
    [Range(0, 1)] public float RewardCurrencyChance = 0.55f;
    [Tooltip("클리어 보상 가중치: HP 회복")]
    [Range(0, 1)] public float RewardHPChance = 0.25f;
    [Tooltip("런 재화 보상 수량")]
    public int RewardCurrencyAmount = 6;
    [Tooltip("HP 회복 보상 수량")]
    public int RewardHPAmount = 8;
    [Header("대형룸 전용 풀")]
    [Tooltip("대형룸 클리어 보상 가중치: 아티팩트")]
    [Range(0, 1)] public float LargeArtifactChance = 0.40f;
    [Tooltip("대형룸 클리어 보상 가중치: 런 재화")]
    [Range(0, 1)] public float LargeCurrencyChance = 0.40f;
    [Tooltip("대형룸 클리어 보상 가중치: HP 회복")]
    [Range(0, 1)] public float LargeHPChance = 0.20f;
    [Tooltip("대형룸 런 재화 보상 수량")]
    public int LargeCurrencyAmount = 12;
    [Tooltip("대형룸 HP 회복 보상 수량")]
    public int LargeHPAmount = 16;
    [Header("진행")]
    [Tooltip("웨이브 등장 예고 시간 (초). 첫 웨이브 포함. 예고 중에는 적 없음")]
    public float WaveTransitionWarningDuration = 1.0f;
    [Tooltip("웨이브 몬스터가 등장 직후 접촉 피해를 주지 않는 시간 (초)")]
    public float SpawnContactDamageGrace = 0.25f;
    [Tooltip("보상이 나타난 뒤 주울 수 있게 되기까지 시간 (초)")]
    public float RewardPickupEnableDelay = 0.25f;
    [Tooltip("자동 생성 웨이브에서 몬스터 1마리당 런 재화 드롭")]
    public int MonsterCurrencyDropDefault = 1;
    [Tooltip("미방문 룸 가림이 걷히는 시간 (초)")]
    public float RoomRevealFadeDuration = 0.25f;
    [Tooltip("스폰점 없을 때 대체 스폰에서 플레이어 주변 제외 반경 (H)")]
    public float FirstEntrySafeRadius = 2f;
}

[Serializable]
public class ShopBalance
{
    [Header("03-3 상점")]
    [Tooltip("소형 회복 가격 범위 (최소, 최대)")]
    public Vector2Int SmallHealPrice = new Vector2Int(4, 6);
    [Tooltip("소형 회복 HP")]
    public int SmallHealAmount = 8;
    [Tooltip("대형 회복 가격 범위 (최소, 최대)")]
    public Vector2Int LargeHealPrice = new Vector2Int(7, 10);
    [Tooltip("대형 회복 HP")]
    public int LargeHealAmount = 16;
    [Tooltip("영구 재화 묶음 수량")]
    public int PermanentBundleAmount = 2;
    [Tooltip("영구 재화 묶음 가격 범위 (최소, 최대)")]
    public Vector2Int PermanentBundlePrice = new Vector2Int(7, 10);
    [Tooltip("상점 슬롯이 4개일 확률. 아니면 3개")]
    [Range(0, 1)] public float FourSlotChance = 0.5f;
    [Tooltip("추가 슬롯에 영구 재화 묶음이 나올 확률")]
    [Range(0, 1)] public float ExtraSlotBundleChance = 0.5f;
    [Tooltip("회복 슬롯이 대형 회복일 확률")]
    [Range(0, 1)] public float LargeHealChance = 0.5f;
    [Tooltip("일반 아티팩트 가격 범위 (최소, 최대)")]
    public Vector2Int CommonArtifactPrice = new Vector2Int(8, 11);
    [Tooltip("희귀 아티팩트 가격 범위 (최소, 최대)")]
    public Vector2Int RareArtifactPrice = new Vector2Int(14, 18);
    [Tooltip("전설 아티팩트 가격 범위 (최소, 최대)")]
    public Vector2Int LegendaryArtifactPrice = new Vector2Int(24, 30);
    [Tooltip("상점 리롤 비용 (런 재화)")]
    public int RerollCost = 6;
}

[Serializable]
public class GambleBalance
{
    [Header("03-4 도박룸")]
    [Tooltip("도박 1회당 런 재화 소모")]
    public int CurrencyCostPerAttempt = 3;
    [Tooltip("도박 첫 시도의 아티팩트 확률")]
    [Range(0, 1)] public float BaseArtifactChance = 0.25f;
    [Tooltip("실패마다 더해지는 확률. 현재 확률 = 기본 + 이 값 x 실패 횟수 (최대 1)")]
    [Range(0, 1)] public float PityIncrement = 0.20f;
}

[Serializable]
public class SecretRoomBalance
{
    [Header("03-5 비밀방 콘텐츠 가중치")]
    [Tooltip("비밀방 콘텐츠 가중치: 런 재화")]
    public float CurrencyWeight = 30f;
    [Tooltip("비밀방 콘텐츠 가중치: HP 회복")]
    public float HPWeight = 20f;
    [Tooltip("비밀방 콘텐츠 가중치: 아티팩트")]
    public float ArtifactWeight = 35f;
    [Tooltip("비밀방 콘텐츠 가중치: 도박")]
    public float GambleWeight = 15f;
    [Tooltip("두 번째 UI 제작 폴백: 도박룸과 비밀방 도박 기능 제외")]
    public bool SecondUIFallback;
}

[Serializable]
public class FloorEndBalance
{
    [Header("03 / 03-6 층 진행")]
    [Tooltip("층 전환 암전이 이 시간보다 길어지면 대기 표시 (초)")]
    public float WaitIndicatorDelay = 1f;
    [Tooltip("층 도착 문구에 쓰는 층 이름 (1층부터)")]
    public string[] FloorNames = { "1층 임시이름", "2층 임시이름", "3층 임시이름" };
}

[Serializable]
public class BossBalance
{
    [Header("05 보스 (더미)")]
    [Tooltip("BossDefinition에 HP가 없을 때 쓰는 기본 HP")]
    public int HP = 1200;
    [Tooltip("HP가 최대치의 이 비율 이하가 되면 2페이즈")]
    [Range(0, 1)] public float PhaseTransitionThreshold = 0.5f;
    [Tooltip("사망 모션 길이 (초). 끝나면 몸체 제거. 개발 착수값")]
    public float DeathMotionDuration = 1.5f;
    [Tooltip("등장 연출 길이 (초). 개발 착수값")]
    public float IntroDuration = 1.5f;
    [Tooltip("보스 몸체 접촉 피해")]
    public int ContactDamage = 8;
    [Tooltip("보스 피격, 접촉 반경 (H)")]
    public float BodyRadius = 0.9f;
}

[Serializable]
public class ArtifactBalance
{
    [Header("07 아티팩트")]
    [Tooltip("아티팩트 장착 슬롯 수")]
    public int SlotCount = 6;
    [Tooltip("같은 세트를 이 개수 이상 장착하면 1단계 세트 효과")]
    public int Tier1Threshold = 2;
    [Tooltip("같은 세트를 이 개수 이상 장착하면 2단계 세트 효과")]
    public int Tier2Threshold = 4;
    [Tooltip("등급 추첨 가중치: 일반")]
    public float CommonWeight = 65f;
    [Tooltip("등급 추첨 가중치: 희귀")]
    public float RareWeight = 27f;
    [Tooltip("등급 추첨 가중치: 전설")]
    public float LegendaryWeight = 8f;
    [Tooltip("장착 중인 미완성 세트가 있을 때 그 세트 ID로 쏠려 뽑을 확률")]
    [Range(0, 1)] public float SetBiasTriggerChance = 0.5f;
    [Tooltip("아티팩트 획득 1회당 리롤 최대 횟수")]
    public int RerollMaxCount = 2;
    [Tooltip("그중 무료 리롤 횟수")]
    public int RerollFreeCount = 1;
    [Tooltip("필드 드롭 시 벽과 다른 상호작용물을 피할 자리를 찾는 반경 (H)")]
    public float FieldDropSearchRadius = 0.75f;
    [Tooltip("무기 특화 세트 규칙 (07 §4.14): Tier1부터 다른 두 무기 잠금 + 특화 세트 1개만. 끄면 잠그지 않고 여러 특화 세트 동시 활성 가능")]
    public bool WeaponSpecializationRules = false;
    [Tooltip("라이플 연속 명중 스택 유지 시간(초). 마지막 명중부터 이 시간이 지나거나 다른 대상을 맞히면 스택 초기화")]
    public float RifleStackDuration = 3f;
}

[Serializable]
public class MetaBalance
{
    [Header("06 런, 영구 진행")]
    [Tooltip("룸 1개 클리어당 영구 재화")]
    public int PermanentPerRoomCleared = 2;
    [Tooltip("층 클리어 보너스 영구 재화 (1층, 2층, 3층)")]
    public int[] FloorClearBonus = { 4, 7, 10 };
    [Tooltip("아티팩트 1개 해금 비용 (영구 재화)")]
    public int ArtifactUnlockCost = 30;
    [Header("06-1 성장 포인트")]
    [Tooltip("n번째 성장 포인트 가격 (1번째부터). 개수가 곧 구매 상한")]
    public int[] GrowthPointPrices = { 20, 20, 25, 30, 35, 40, 45, 55, 60, 70, 80, 90, 100 };
    [Tooltip("성장 트리 (공격, 회피, 유틸). 노드 k는 k포인트를 쓰고 아래 노드가 모두 배치돼야 배치 가능")]
    public GrowthCategory[] GrowthTree =
    {
        new GrowthCategory("공격", new[]
        {
            new GrowthNode("GROWTH_ATK_1", new StatModifier(StatId.CritChance, ModifierKind.Flat, 0.08f)),
            new GrowthNode("GROWTH_ATK_2", new StatModifier(StatId.CritChance, ModifierKind.Flat, 0.05f), new StatModifier(StatId.CritDamage, ModifierKind.Flat, 0.3f)),
            new GrowthNode("GROWTH_ATK_3", new StatModifier(StatId.CritChance, ModifierKind.Flat, 0.06f), new StatModifier(StatId.CritDamage, ModifierKind.Flat, 0.3f)),
            new GrowthNode("GROWTH_ATK_4", new StatModifier(StatId.WeaponDamageAll, ModifierKind.Percent, 0.09f), new StatModifier(StatId.CritDamage, ModifierKind.Flat, 0.3f)),
        }),
        new GrowthCategory("회피", new[]
        {
            new GrowthNode("GROWTH_DOD_1", new StatModifier(StatId.DodgeChargeTime, ModifierKind.Percent, -0.1f)),
            new GrowthNode("GROWTH_DOD_2", new StatModifier(StatId.MaxHP, ModifierKind.Percent, 0.05f)),
            new GrowthNode("GROWTH_DOD_3", new StatModifier(StatId.MaxHP, ModifierKind.Percent, 0.08f)),
            new GrowthNode("GROWTH_DOD_4", new StatModifier(StatId.DodgeMaxCharge, ModifierKind.Flat, 1f)),
        }),
        new GrowthCategory("유틸", new[]
        {
            new GrowthNode("GROWTH_UTL_1", new StatModifier(StatId.ReloadTimeAll, ModifierKind.Percent, -0.15f), new StatModifier(StatId.MagazineSizeAll, ModifierKind.Percent, 0.1f)),
            new GrowthNode("GROWTH_UTL_2", new StatModifier(StatId.MoveSpeed, ModifierKind.Percent, 0.1f)),
            new GrowthNode("GROWTH_UTL_3", new StatModifier(StatId.FireIntervalAll, ModifierKind.Percent, -0.1f)),
            new GrowthNode("GROWTH_UTL_4", new StatModifier(StatId.FireIntervalAll, ModifierKind.Percent, -0.12f), new StatModifier(StatId.MoveSpeed, ModifierKind.Percent, 0.05f), new StatModifier(StatId.ReloadTimeAll, ModifierKind.Percent, -0.1f)),
        }),
    };
    [Tooltip("런 시작 시 페이드 아웃 시간 (초)")]
    public float RunEntryFadeOut = 0.18f;
    [Tooltip("런 시작 시 페이드 인 시간 (초)")]
    public float RunEntryFadeIn = 0.18f;
    [Tooltip("사망 후 결과 화면까지 전환 시간 (초)")]
    public float DeathResultTransitionDuration = 0.75f;
    [Tooltip("포기 후 결과 화면 페이드 시간 (초)")]
    public float AbandonResultFadeDuration = 0.3f;
    [Tooltip("엔딩 연출 길이 (초)")]
    public float EndingDuration = 12f;
    [Tooltip("엔딩 마지막 문구")]
    public string EndingLastLine = "이곳에 숨겨진 보물이 더 있을 것 같다";
}

[Serializable]
public class FeedbackBalance
{
    [Header("09 연출 (진폭H / 지속초)")]
    [Tooltip("라이플 명중 흔들림 (진폭 H, 지속 초)")]
    public Vector2 RifleHitShake = new Vector2(0.008f, 0.04f);
    [Tooltip("권총 명중 흔들림 (진폭 H, 지속 초)")]
    public Vector2 PistolHitShake = new Vector2(0.018f, 0.06f);
    [Tooltip("샷건 명중 흔들림 (진폭 H, 지속 초)")]
    public Vector2 ShotgunHitShake = new Vector2(0.04f, 0.08f);
    [Tooltip("플레이어 피격 흔들림 (진폭 H, 지속 초)")]
    public Vector2 PlayerHitShake = new Vector2(0.08f, 0.12f);
    [Tooltip("이 간격 안에 들어온 흔들림 요청은 더 강한 것만 반영 (초)")]
    public float HitShakeRetriggerInterval = 0.05f;
    [Tooltip("몬스터 피격 흰색 번쩍임 시간 (초)")]
    public float MonsterWhiteFlashDuration = 0.06f;
    [Tooltip("처치 히트스톱 시간 (초)")]
    public float KillHitstopDuration = 0.04f;
    [Tooltip("플레이어 피격 색 변화 시간 (초)")]
    public float PlayerHitTintDuration = 0.06f;
    [Tooltip("피격 무적 중 깜빡임 주기 (초)")]
    public float PlayerInvincibleBlinkPeriod = 0.08f;
    [Tooltip("세트 효과 발동 알림 표시 시간 (초)")]
    public float SetActivationToastDuration = 1.0f;
}

[Serializable]
public class UIBalance
{
    [Header("08 / 10 UI")]
    [Tooltip("몬스터 HP바가 마지막 피격 후 사라지기까지 시간 (초)")]
    public float MonsterHPBarFadeDelay = 1.25f;
    [Tooltip("층 도착 문구 시간 (페이드 인, 유지, 페이드 아웃) 초")]
    public Vector3 FloorArrivalTextDuration = new Vector3(0.2f, 1.3f, 0.3f);
    [Tooltip("UI 기준 해상도")]
    public Vector2Int ReferenceResolution = new Vector2Int(1920, 1080);
    [Tooltip("HUD 미니맵이 보여주는 범위 (H). 1배 룸 하나와 출구가 들어오는 정도. 개발 착수값")]
    public float MinimapHUDViewRange = 20f;
    [Tooltip("설정의 화면 흔들림 기본값 (0~100)")]
    [Range(0, 100)] public float ScreenShakeDefault = 75f;
    [Tooltip("HUD HP바 길이: 최대 HP 1당 픽셀. 최대 HP가 늘면 바가 길어짐")]
    public float HPBarWidthPerHP = 8f;
    [Tooltip("HUD HP바 길이 범위 (최소, 최대) 픽셀")]
    public Vector2 HPBarWidthRange = new Vector2(120f, 900f);
}

// 06-1 성장 트리 한 카테고리. 노드는 아래(1번)부터 위(4번) 순서
[Serializable]
public class GrowthCategory
{
    public string Name;
    public GrowthNode[] Nodes;

    public GrowthCategory() { }

    public GrowthCategory(string name, GrowthNode[] nodes)
    {
        Name = name;
        Nodes = nodes;
    }
}

[Serializable]
public class GrowthNode
{
    public string Id;
    [Tooltip("영구 강화 수정값. 비율(Percent)은 아티팩트 비율과 합산됨")]
    public StatModifier[] Modifiers;

    public GrowthNode() { }

    public GrowthNode(string id, params StatModifier[] modifiers)
    {
        Id = id;
        Modifiers = modifiers;
    }
}
