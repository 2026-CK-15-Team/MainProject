using UnityEngine;

public static class PlayerCombatStats
{
    private static PlayerCombatData data;
    private static float critChanceBonus;
    private static float critDamageBonus;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetOnPlay()
    {
        data = null;
        critChanceBonus = 0f;
        critDamageBonus = 0f;
    }

    public static void Initialize(PlayerCombatData combatData)
    {
        data = combatData;
        critChanceBonus = 0f;
        critDamageBonus = 0f;
        //Debug.Log(data != null ? $"[CombatStats] 에셋 연결됨, BaseCritChance={data.BaseCritChance}" : "[CombatStats] 에셋이 null — 폴백 기본값(5%) 사용 중");
    }

    private static float BaseCritChance => data != null ? data.BaseCritChance : 0.05f;
    private static float BaseCritDamageMultiplier => data != null ? data.BaseCritDamageMultiplier : 1.5f;

    public static float CritChance => Mathf.Clamp01(BaseCritChance + critChanceBonus);
    public static float CritDamageMultiplier => BaseCritDamageMultiplier + critDamageBonus;

    public static void AddCritChancePercent(float percent) => critChanceBonus += percent;
    public static void AddCritDamagePercent(float percent) => critDamageBonus += percent;
}