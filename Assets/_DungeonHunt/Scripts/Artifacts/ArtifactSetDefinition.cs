using System.Collections.Generic;
using UnityEngine;

// 세트 1종 (07 §9.1). Tier 효과는 기획 미정 → 수정값 목록이 비어 있으면 효과 없음.
[CreateAssetMenu(fileName = "ArtifactSet", menuName = "DungeonHunt/Artifact Set")]
public class ArtifactSetDefinition : ScriptableObject
{
    public string Id;
    public string DisplayName;
    public Color Color = Color.white;
    [Tooltip("무기 특화 세트면 켬. Tier1부터 나머지 두 무기를 잠금 (07 §4.14)")]
    public bool IsWeaponSpecialization;
    public WeaponType SpecializedWeapon;
    public List<StatModifier> Tier1Modifiers = new List<StatModifier>();
    public List<StatModifier> Tier2Modifiers = new List<StatModifier>();
    [Tooltip("도감 그룹 나열 순서 (기획 미정)")]
    public int CodexGroupOrder;

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(Id)) Id = name;
    }
}
