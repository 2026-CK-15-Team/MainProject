using System.Collections.Generic;
using UnityEngine;

public enum ArtifactGrade
{
    Common,     // 일반
    Rare,       // 희귀
    Legendary,  // 전설
}

// 해금 출처 3가지 (06 §5.3). 아티팩트 하나는 하나에만 속함.
public enum ArtifactUnlockSource
{
    Initial,
    Currency,
    Progress,
}

// 원본 획득 경로 (07 §3). 등급 제한과 리롤 제공 여부를 정함.
public enum AcquisitionOrigin
{
    TreasureRoom,
    CombatRoom,
    SecretRoom,
    EliteGate,
    Shop,
    Gamble,
}

public enum SetTier
{
    Inactive,
    Tier1,
    Tier2,
}

public static class ArtifactGradeUtility
{
    public static string Display(ArtifactGrade grade) => grade switch
    {
        ArtifactGrade.Common => "일반",
        ArtifactGrade.Rare => "희귀",
        _ => "전설",
    };

    public static Color Color(ArtifactGrade grade) => grade switch
    {
        ArtifactGrade.Common => new Color(0.8f, 0.8f, 0.8f),
        ArtifactGrade.Rare => new Color(0.35f, 0.6f, 1f),
        _ => new Color(1f, 0.65f, 0.15f),
    };

    public static bool IsFieldOrigin(AcquisitionOrigin origin)
        => origin != AcquisitionOrigin.Shop && origin != AcquisitionOrigin.Gamble;
}

public enum ProgressCondition
{
    ReachFloor2,
    ReachFloor3,
    FirstClear,
    SecondClear,
}

// 스탯 수정값으로 못 하는 특수 효과 (아티팩트 개별 효과, 세트 Tier 효과 공용).
// 같은 종류 여러 개는 합연산(값 더함). 교체 직후 피해 배율만 곱연산(배율끼리 곱함).
public enum ArtifactEffectKind
{
    None,
    SwapDamageWindow,     // 무기 교체 직후 Duration초 모든 무기 피해 x Value (곱)
    PistolRicochet,       // 권총 탄이 적 탄과 부딪히면 Value 확률로 둘 다 사라짐 (투사체 상쇄)
    DodgeDamageBuff,      // 회피 시 Duration초 모든 무기 피해 +Value (0.2 = +20%)
    JustDodgeInvincible,  // 저스트회피 성공 시 Duration초 무적
    RifleStackDamage,     // 라이플로 같은 대상 연속 명중 시 스택마다 피해 +Value
    RifleFiringMoveSpeed, // 라이플 사격 중 이동속도 +Value
    SwapEnhancedRounds,   // 수동 무기 교체 시 교체한 무기의 현재 탄창 피해 +Value (강화탄, 재장전하면 해제)
    SwapRefill,           // 수동 무기 교체 시 교체한 무기 탄창의 Value 비율만큼 즉시 회복
}

[System.Serializable]
public struct ArtifactEffect
{
    public ArtifactEffectKind Kind;
    [Tooltip("효과 값. 비율은 0.2 = 20%, 배율은 1.08 = x1.08")]
    public float Value;
    [Tooltip("지속 시간(초). 시간이 있는 효과만")]
    public float Duration;

    public ArtifactEffect(ArtifactEffectKind kind, float value, float duration = 0f)
    {
        Kind = kind;
        Value = value;
        Duration = duration;
    }

    public override string ToString() => Kind switch
    {
        ArtifactEffectKind.SwapDamageWindow => $"교체 직후 {Duration:0.#}초 모든 무기 피해 x{Value:0.##}",
        ArtifactEffectKind.PistolRicochet => $"권총 탄이 적 탄과 부딪히면 {Value * 100f:0}% 확률로 함께 사라짐",
        ArtifactEffectKind.DodgeDamageBuff => $"회피 시 {Duration:0.#}초 피해 +{Value * 100f:0}%",
        ArtifactEffectKind.JustDodgeInvincible => $"저스트회피 성공 시 {Duration:0.#}초 무적",
        ArtifactEffectKind.RifleStackDamage => $"라이플로 같은 대상 연속 명중 시 스택마다 피해 +{Value * 100f:0}%",
        ArtifactEffectKind.RifleFiringMoveSpeed => $"라이플 사격 중 이동속도 +{Value * 100f:0}%",
        ArtifactEffectKind.SwapEnhancedRounds => $"무기 교체 시 그 무기의 현재 탄창 피해 +{Value * 100f:0}%",
        ArtifactEffectKind.SwapRefill => $"무기 교체 시 그 무기 탄창 {Value * 100f:0}% 즉시 회복",
        _ => "",
    };
}
