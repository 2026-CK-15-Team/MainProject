using UnityEngine;

public static class RunProgressState
{
    public static ArtifactSetType? TreasureSetType;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetOnPlay() => TreasureSetType = null;

    public static void Reset()
    {
        Debug.Log("[RunProgressState] 초기화됨 (목표세트 기록 삭제)");
        TreasureSetType = null;
    }
}