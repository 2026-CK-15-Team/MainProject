using UnityEngine;

[CreateAssetMenu(fileName = "WeaponScriptable", menuName = "Scriptable Objects/WeaponScriptable")]
public class WeaponScriptable : ScriptableObject
{
    public WeaponType WeaponType;
    public string WeaponName;
    public int MaxAmmo;
    public float ReloadTime;
    public float SwapReadyDelay;
    public float FireInterval;
    public int Damage;
    public float ProjectileSpeed;
    public float Range;           // 샷건: 원추 판정 반경
    public float ConeAngle;       // 샷건: 원추 각도(도)
}
