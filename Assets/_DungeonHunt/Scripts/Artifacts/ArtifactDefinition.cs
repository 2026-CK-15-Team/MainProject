using System.Collections.Generic;
using UnityEngine;

// 아티팩트 1개. ID마다 고정 등급과 효과 (07 §4.1).
// 효과는 스탯 수정값 목록 { 대상 스탯, 분류, 값 } (07 §8.1). 개별 효과 콘텐츠는 기획 미정.
[CreateAssetMenu(fileName = "Artifact", menuName = "DungeonHunt/Artifact")]
public class ArtifactDefinition : ScriptableObject
{
    [Tooltip("고유 ID. 세이브에 저장되므로 한 번 정하면 바꾸지 말 것")]
    public string Id;
    public string DisplayName;
    public ArtifactGrade Grade;
    [Tooltip("소속 세트. 비우면 범용 아티팩트")]
    public ArtifactSetDefinition Set;
    public ArtifactUnlockSource UnlockSource = ArtifactUnlockSource.Initial;
    public List<StatModifier> Modifiers = new List<StatModifier>();
    [TextArea] public string Description;
    public Sprite Icon;
    [Tooltip("도감 그룹 안 고정 순서")]
    public int CodexOrder;
    [Tooltip("테스트 전용 더미. 기획 확정 콘텐츠가 아님")]
    public bool IsTestPlaceholder;

    public bool IsUniversal => Set == null;

    public string EffectText
    {
        get
        {
            var parts = new List<string>();
            foreach (StatModifier modifier in Modifiers) parts.Add(modifier.ToString());
            string mods = string.Join(", ", parts);
            if (string.IsNullOrEmpty(Description)) return mods;
            return string.IsNullOrEmpty(mods) ? Description : Description + "\n" + mods;
        }
    }

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(Id)) Id = name;
    }
}
