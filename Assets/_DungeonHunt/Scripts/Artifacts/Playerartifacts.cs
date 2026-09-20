using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PlayerArtifacts : MonoBehaviour
{
    public const int SlotCount = 6;
    public const int Tier1Threshold = 2;

    [SerializeField] private PlayerMovement movement;
    [SerializeField] private WeaponController weaponController;
    [SerializeField] private PlayerCombatData combatData;

    private readonly List<ArtifactDefinition> equipped = new();
    private readonly Dictionary<ArtifactSetType, bool> tier1Active = new();

    public IReadOnlyList<ArtifactDefinition> Equipped => equipped;

    public event Action<ArtifactSetType, bool> SetTierChanged;

    private void Awake()
    {
        PlayerCombatStats.Initialize(combatData);
    }

    public bool TryEquip(ArtifactDefinition definition)
    {
        if (definition == null)
        {
            Debug.Log("[Artifact] 유효한 후보가 없어 획득 취소");
            return false;
        }

        if (equipped.Any(a => a.Id == definition.Id))
        {
            Debug.Log($"[Artifact] 이미 보유 중인 ID: {definition.Id}, 획득 실패");
            return false;
        }

        if (equipped.Count >= SlotCount)
        {
            Debug.Log("슬롯이 가득 참 — 획득 대기 모드 미구현, 지금은 획득 실패 처리");
            return false;
        }

        equipped.Add(definition);
        ApplyIndividualEffect(definition);
        RecheckSetTier(definition.SetType);

        Debug.Log($"[Artifact] 획득: {definition.DisplayName} ({definition.Grade}, {definition.SetType})");
        PlaytestLogger.Log("ArtifactEquip", $"{definition.Id},{definition.Grade},{definition.SetType}");
        return true;
    }

    private int CountBySet(ArtifactSetType setType)
    {
        int count = 0;
        foreach (var a in equipped)
            if (a.SetType == setType) count++;
        return count;
    }

    private void RecheckSetTier(ArtifactSetType setType)
    {
        bool wasActive = tier1Active.TryGetValue(setType, out var active) && active;
        bool nowActive = CountBySet(setType) >= Tier1Threshold;
        if (wasActive == nowActive) return;

        tier1Active[setType] = nowActive;
        PlaytestLogger.Log("Tier1", $"{setType},active={nowActive}");
        SetTierChanged?.Invoke(setType, nowActive);
        ApplyTier1(setType, nowActive);
    }

    private void ApplyIndividualEffect(ArtifactDefinition definition)
    {
        switch (definition.Id)
        {
            case ArtifactId.PT_PISTOL_01: // 이동속도 +10%
                movement.MoveMaxSpeed *= 1.10f;
                break;
            case ArtifactId.PT_PISTOL_02: // 회피 충전시간 -20%
                movement.DodgeChargeTime *= 0.80f;
                break;
            case ArtifactId.PT_RIFLE_01: // 라이플 피해 +10%
                weaponController.GetWeapon(WeaponType.Rifle)?.AddDamagePercent(0.10f);
                break;
            case ArtifactId.PT_RIFLE_02: // 라이플 사거리 +15%
                weaponController.GetWeapon(WeaponType.Rifle)?.AddRangePercent(0.15f);
                break;
            case ArtifactId.PT_SHOTGUN_01: // 샷건 탄창 +2
                weaponController.GetWeapon(WeaponType.Shotgun)?.AddMagazineBonus(2);
                break;
            case ArtifactId.PT_SHOTGUN_02: // 샷건 재장전시간 -20%
                weaponController.GetWeapon(WeaponType.Shotgun)?.AddReloadTimePercent(-0.20f);
                break;
            case ArtifactId.PT_SWAP_01: // 모든 무기 재장전시간 -15%
                weaponController.GetWeapon(WeaponType.Pistol)?.AddReloadTimePercent(-0.15f);
                weaponController.GetWeapon(WeaponType.Rifle)?.AddReloadTimePercent(-0.15f);
                weaponController.GetWeapon(WeaponType.Shotgun)?.AddReloadTimePercent(-0.15f);
                break;
            case ArtifactId.PT_SWAP_02: // 치명타확률 +10%p
                PlayerCombatStats.AddCritChancePercent(0.10f);
                break;
        }
    }

    private void ApplyTier1(ArtifactSetType setType, bool active)
    {
        switch (setType)
        {
            case ArtifactSetType.Pistol:
                LockOthers(WeaponType.Pistol, active);
                var pistol = weaponController.GetWeapon(WeaponType.Pistol);
                pistol?.AddFireIntervalPercent(active ? -0.25f : 0.25f);
                if (pistol != null) pistol.CanDeflectProjectiles = active;
                break;

            case ArtifactSetType.Rifle:
                LockOthers(WeaponType.Rifle, active);
                var rifle = weaponController.GetWeapon(WeaponType.Rifle);
                if (rifle != null)
                {
                    rifle.RifleStack.Enabled = active;
                    if (!active) rifle.RifleStack.ClearTarget();
                }
                break;

            case ArtifactSetType.Shotgun:
                LockOthers(WeaponType.Shotgun, active);
                weaponController.GetWeapon(WeaponType.Shotgun)?.AddRangePercent(active ? 2.0f : -2.0f); // +200%
                break;

            case ArtifactSetType.Swap:
                weaponController.SetSwapTier1Active(active);
                if (!active) weaponController.ClearAllEnhancedAmmo();
                break;
        }
    }

    private void LockOthers(WeaponType specialized, bool locked)
    {
        foreach (WeaponType type in Enum.GetValues(typeof(WeaponType)))
        {
            if (type == specialized) continue;
            weaponController.SetLocked(type, locked);
        }

        if (locked)
        {
            weaponController.ClearAllEnhancedAmmo();
            if (weaponController.CurrentWeapon.Definition.WeaponType != specialized)
                weaponController.ForceEquip(specialized);
        }
    }
}