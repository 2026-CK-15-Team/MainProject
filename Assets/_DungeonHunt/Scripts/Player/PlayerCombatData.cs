using UnityEngine;

[CreateAssetMenu(fileName = "PlayerCombatData", menuName = "Scriptable Objects/PlayerCombatData")]
public class PlayerCombatData : ScriptableObject
{
    public float BaseCritChance = 0.05f;
    public float BaseCritDamageMultiplier = 1.5f;
}
