using UnityEngine;

public enum ArtifactId
{
    PT_PISTOL_01, PT_PISTOL_02,
    PT_RIFLE_01, PT_RIFLE_02,
    PT_SHOTGUN_01, PT_SHOTGUN_02,
    PT_SWAP_01, PT_SWAP_02
}

public enum ArtifactGrade { Common, Rare }

public enum ArtifactSetType { Pistol, Rifle, Shotgun, Swap }

[CreateAssetMenu(fileName = "ArtifactDefinition", menuName = "Scriptable Objects/ArtifactDefinition")]
public class ArtifactDefinition : ScriptableObject
{
    public ArtifactId Id;
    public string DisplayName;
    public ArtifactGrade Grade;
    public ArtifactSetType SetType;
}
