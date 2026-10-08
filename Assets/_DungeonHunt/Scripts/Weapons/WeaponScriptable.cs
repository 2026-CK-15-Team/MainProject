using UnityEngine;

// 무기 1종 데이터 (01-2 §7). 기존 필드 이름은 에셋 호환 때문에 그대로 둠.
// 값은 PrototypeDefault v0.2 (TODO_BALANCE). 최종값은 PlayerStats가 수정값과 합쳐서 줌.
[CreateAssetMenu(fileName = "WeaponScriptable", menuName = "Scriptable Objects/WeaponScriptable")]
public class WeaponScriptable : ScriptableObject
{
    public WeaponType WeaponType;
    public string WeaponName;
    [Tooltip("탄창 크기 (MagazineSize)")]
    public int MaxAmmo;
    public float ReloadTime;
    [Tooltip("WeaponSwap_ReadyDelay. 교체 직후 발사 유예")]
    public float SwapReadyDelay = 0.12f;
    [Tooltip("연속 발사 사이 최소 시간. 하한 0.04s")]
    public float FireInterval;
    [Tooltip("WeaponBaseDamage")]
    public int Damage;
    [Tooltip("투사체 속도 H/s (권총, 라이플)")]
    public float ProjectileSpeed;
    [Tooltip("최대 사거리 H. 투사체는 이동거리, 샷건은 원추 반경")]
    public float Range;
    [Tooltip("샷건 원추 전체 각도(도)")]
    public float ConeAngle;

    [Header("판정, 넉백")]
    [Tooltip("ProjectileHitRadius (권총/라이플) H")]
    public float ProjectileHitRadius = 0.12f;
    [Tooltip("명중 시 몬스터 목표 밀림 거리 H (KnockbackForce)")]
    public float KnockbackDistance = 0.2f;

    [Header("표시")]
    public Sprite Icon;
    public Sprite WeaponSprite;
    public Color PlaceholderColor = Color.gray;
    public GameObject ProjectilePrefab;
}
