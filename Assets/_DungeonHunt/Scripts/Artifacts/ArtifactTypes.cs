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
