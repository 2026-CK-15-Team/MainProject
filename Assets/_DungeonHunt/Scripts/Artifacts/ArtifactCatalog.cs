using System.Collections.Generic;
using UnityEngine;

// 활성 카탈로그 (주설계 28 / 콘텐츠 폴백 20, 07 §7)
[CreateAssetMenu(fileName = "ArtifactCatalog", menuName = "DungeonHunt/Artifact Catalog")]
public class ArtifactCatalog : ScriptableObject
{
    public List<ArtifactSetDefinition> Sets = new List<ArtifactSetDefinition>();
    public List<ArtifactDefinition> Artifacts = new List<ArtifactDefinition>();

    [Header("진행 해금 조건 연결 (06 §5.3). 비어 있으면 해금 대상 없음")]
    public ArtifactDefinition ReachFloor2;
    public ArtifactDefinition ReachFloor3;
    public ArtifactDefinition FirstClear;
    public ArtifactDefinition SecondClear;

    public ArtifactDefinition Find(string id) => Artifacts.Find(a => a != null && a.Id == id);

    public IEnumerable<ArtifactDefinition> InitialUnlocks()
    {
        foreach (ArtifactDefinition a in Artifacts)
            if (a != null && a.UnlockSource == ArtifactUnlockSource.Initial) yield return a;
    }

    public ArtifactDefinition ProgressLink(ProgressCondition condition) => condition switch
    {
        ProgressCondition.ReachFloor2 => ReachFloor2,
        ProgressCondition.ReachFloor3 => ReachFloor3,
        ProgressCondition.FirstClear => FirstClear,
        _ => SecondClear,
    };
}
