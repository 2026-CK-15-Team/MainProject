using UnityEngine;

public enum AmmoState
{
    Ready,
    Reloading,
}

// 무기 1개의 런타임 상태 (01-1 §5.2). 탄약 상태와 SwapReadyDelay는 서로 독립된 축.
// 재장전 타이머는 장착 여부와 무관하게 흐름 (백그라운드 재장전).
public class WeaponRuntime
{
    public WeaponScriptable Definition { get; }
    public IWeaponFireMode FireMode { get; }
    public WeaponType Type => Definition.WeaponType;

    public int Ammo { get; private set; }
    // 강화탄 (스왑 세트 Tier1). 탄창 전체가 강화 또는 일반 한 가지. 재장전 시작/완료 시 일반탄으로 (01-2 §10)
    public bool IsEnhanced { get; set; }
    public AmmoState State { get; private set; }
    public bool IsLocked { get; set; }

    // 정규화 진행률로 저장 → 재장전 시간이 도중에 바뀌어도 진행률 유지 (01-4 §7)
    private float reloadProgress;
    private float swapDelayRemaining;
    private float fireCooldownRemaining;
    private readonly PlayerStats stats;

    public WeaponRuntime(WeaponScriptable definition, IWeaponFireMode fireMode, PlayerStats stats)
    {
        Definition = definition;
        FireMode = fireMode;
        this.stats = stats;
        Ammo = MaxAmmo;
    }

    public int MaxAmmo => stats != null ? stats.GetWeaponInt(Type, WeaponStat.Magazine) : Definition.MaxAmmo;
    public float ReloadTime => stats != null ? stats.GetWeapon(Type, WeaponStat.ReloadTime) : Definition.ReloadTime;
    public float FireInterval => stats != null ? stats.GetWeapon(Type, WeaponStat.FireInterval) : Definition.FireInterval;
    public float SwapReadyDelay => Mathf.Max(0f, Definition.SwapReadyDelay + (stats != null ? stats.Get(StatId.SwapReadyDelay) : 0f));

    public bool IsReloading => State == AmmoState.Reloading;
    public bool IsSwapDelayed => swapDelayRemaining > 0f;
    public bool IsFireCooldown => fireCooldownRemaining > 0f;
    public float ReloadProgress01 => IsReloading ? Mathf.Clamp01(reloadProgress) : 1f;
    public float SwapDelayProgress01 => SwapReadyDelay <= 0f ? 1f : 1f - Mathf.Clamp01(swapDelayRemaining / SwapReadyDelay);

    // 만료한 타이머 먼저 완료 처리 (01-1 §7.6)
    public void Tick(float dt)
    {
        if (fireCooldownRemaining > 0f) fireCooldownRemaining -= dt;

        if (swapDelayRemaining > 0f)
        {
            swapDelayRemaining -= dt;
            if (swapDelayRemaining <= 0f)
                GameEvents.Raise(GameEvents.WeaponSwapReady, Type);
        }

        if (IsReloading)
        {
            reloadProgress += dt / Mathf.Max(ReloadTime, 0.0001f);
            if (reloadProgress >= 1f)
            {
                Ammo = MaxAmmo;
                State = AmmoState.Ready;
                reloadProgress = 0f;
                IsEnhanced = false;
                GameEvents.Raise(GameEvents.ReloadComplete, Type);
            }
        }

        // 최대치 감소 시 현재 탄창 제한. 증가해도 현재 잔탄은 그대로 (01-4 §3)
        if (Ammo > MaxAmmo) Ammo = MaxAmmo;
    }

    // 새로 장착되는 순간 항상 부여 (휠·숫자키 교체 또는 ForcedUnequip)
    public void OnEquipped()
    {
        swapDelayRemaining = SwapReadyDelay;
    }

    // 탄창 0 자동 진입 또는 R 입력 (최대 미만일 때만). 재장전 중 R은 무시
    public bool TryStartReload()
    {
        if (IsReloading || Ammo >= MaxAmmo) return false;
        State = AmmoState.Reloading;
        reloadProgress = 0f;
        IsEnhanced = false;
        GameEvents.Raise(GameEvents.ReloadStart, Type);
        return true;
    }

    public void CancelReload()
    {
        if (!IsReloading) return;
        State = AmmoState.Ready;
        reloadProgress = 0f;
    }

    public void ConsumeShot()
    {
        Ammo = Mathf.Max(0, Ammo - 1);
        fireCooldownRemaining = FireInterval;
        if (Ammo <= 0)
            TryStartReload();
    }

    // 저스트회피 반환: 빈 공간까지만 (01-2 §5.6)
    public int RefundAmmo(int amount)
    {
        int before = Ammo;
        Ammo = Mathf.Min(MaxAmmo, Ammo + amount);
        return Ammo - before;
    }

    // 층 전환: 탄창 최대, 재장전 취소, SwapReadyDelay 없음 (01-1 §4.4)
    public void RefillForNewFloor()
    {
        Ammo = MaxAmmo;
        State = AmmoState.Ready;
        reloadProgress = 0f;
        swapDelayRemaining = 0f;
        IsEnhanced = false;
    }

    public void ResetForNewRun()
    {
        RefillForNewFloor();
        fireCooldownRemaining = 0f;
        IsLocked = false;
    }
}
