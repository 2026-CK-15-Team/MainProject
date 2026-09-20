using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ArtifactCatalogData", menuName = "Scriptable Objects/ArtifactCatalogData")]
public class ArtifactCatalogData : ScriptableObject
{
    public List<ArtifactDefinition> AllArtifacts = new();
}
