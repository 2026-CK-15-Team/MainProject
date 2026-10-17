using System.Collections.Generic;
using UnityEngine;

// 세트 1종 (07 §9.1). Tier2는 Tier1에 더해짐 (합연산 누적).
// 예: 올인 Tier1 최대 HP -50%, Tier2 -30% 추가 → Tier2 활성 시 -80%
[CreateAssetMenu(fileName = "ArtifactSet", menuName = "DungeonHunt/Artifact Set")]
public class ArtifactSetDefinition : ScriptableObject
{
    public string Id;
    public string DisplayName;
    public Color Color = Color.white;
    [Tooltip("무기 특화 세트면 켬. GameBalance.Artifact.WeaponSpecializationRules가 켜져 있을 때만 Tier1부터 나머지 두 무기를 잠금 (07 §4.14)")]
    public bool IsWeaponSpecialization;
    public WeaponType SpecializedWeapon;
    public List<StatModifier> Tier1Modifiers = new List<StatModifier>();
    public List<StatModifier> Tier2Modifiers = new List<StatModifier>();
    [Tooltip("Tier1 특수 효과")]
    public List<ArtifactEffect> Tier1Effects = new List<ArtifactEffect>();
    [Tooltip("Tier2 특수 효과 (Tier1에 더해짐)")]
    public List<ArtifactEffect> Tier2Effects = new List<ArtifactEffect>();
    [Tooltip("도감 그룹 나열 순서 (기획 미정)")]
    public int CodexGroupOrder;

    // UI 표기용 Tier 효과 문구 (이 Tier에서 추가되는 것만)
    public string TierText(SetTier tier)
    {
        var parts = new List<string>();
        List<StatModifier> mods = tier == SetTier.Tier2 ? Tier2Modifiers : Tier1Modifiers;
        List<ArtifactEffect> effects = tier == SetTier.Tier2 ? Tier2Effects : Tier1Effects;
        foreach (StatModifier m in mods) parts.Add(m.ToString());
        foreach (ArtifactEffect e in effects) parts.Add(e.ToString());
        return string.Join(", ", parts);
    }

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(Id)) Id = name;
    }
}
