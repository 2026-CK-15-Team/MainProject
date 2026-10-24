using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// 08 §4 인게임 상시 HUD
// 좌상단: HP, 회피 충전, 런전용 재화 / 우상단: 미니맵 / 우하단: 무기, 탄약, 재장전, 휠·1 2 3 전환
// 하단 중앙: 활성 세트 / 상단 중앙: 층 도착 안내, 보스 체력바
public class HudView : UIView
{
    private RectTransform hpBar;
    private Image hpFill, hpTrail;
    private Text hpText;
    private RectTransform dodgeRow;
    private readonly List<Image> dodgePips = new List<Image>();
    private Text currencyText;

    private readonly WeaponSlotWidget[] weaponWidgets = new WeaponSlotWidget[3];
    private Text ammoText;
    private Text setsText;

    private CanvasGroup floorArrival;
    private Text floorArrivalText;
    private float floorArrivalStart = -100f;
    private float floorArrivalPausedAt = -1f;

    private GameObject bossBar;
    private Image bossFill;
    private Text bossName;
    private FinalBoss boss;

    private Text centerToast;
    private float centerToastUntil;
    private readonly Queue<(string text, float duration)> setToasts = new Queue<(string, float)>();
    private Text setToast;
    private float setToastUntil;

    private GameObject waitIndicator;

    public MinimapWidget Minimap { get; private set; }

    private class WeaponSlotWidget
    {
        public Image Background, Icon, ReloadFill, SwapFill, Lock;
        public Outline EnhancedOutline;
        public Text Label;
    }

    private GameObject rifleStackRoot;
    private Text rifleStackText;
    private Image rifleStackFill;

    protected override void Build(RectTransform root)
    {
        // 좌상단
        // HP 게이지: 길이는 최대 HP에 비례, 깎인 부분은 잠깐 밝게 남았다가 줄어듦
        Image hpBack = UIFactory.Panel("HP", root, new Color(0.3f, 0.12f, 0.14f, 0.9f));
        hpBar = hpBack.rectTransform;
        UIFactory.Place(hpBar, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -36f), new Vector2(400f, 40f));
        hpTrail = UIFactory.Panel("Trail", hpBar, new Color(1f, 0.85f, 0.85f, 0.9f));
        UIFactory.Stretch(hpTrail.rectTransform, 3f);
        hpTrail.type = Image.Type.Filled;
        hpTrail.fillMethod = Image.FillMethod.Horizontal;
        hpFill = UIFactory.Panel("Fill", hpBar, new Color(0.95f, 0.25f, 0.3f));
        UIFactory.Stretch(hpFill.rectTransform, 3f);
        hpFill.type = Image.Type.Filled;
        hpFill.fillMethod = Image.FillMethod.Horizontal;
        hpText = UIFactory.Label("Text", hpBar, "", 24, TextAnchor.MiddleCenter);
        UIFactory.Stretch(hpText.rectTransform);
        dodgeRow = UIFactory.CreateRect("Dodge", root);
        UIFactory.Place(dodgeRow, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -92f), new Vector2(400f, 22f));
        currencyText = UIFactory.Label("Currency", root, "", 30, TextAnchor.MiddleLeft, UIFactory.AccentColor);
        UIFactory.Place(currencyText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -124f), new Vector2(400f, 40f));

        // 우상단 미니맵
        Minimap = new MinimapWidget();
        Minimap.Create(root);

        // 우하단 무기
        for (int i = 0; i < 3; i++)
        {
            var w = new WeaponSlotWidget();
            w.Background = UIFactory.Panel("Weapon" + i, root, UIFactory.PanelLight);
            UIFactory.Place(w.Background.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f - (2 - i) * 120f, 40f), new Vector2(110f, 110f));
            w.Icon = UIFactory.Panel("Icon", w.Background.transform, Color.gray);
            UIFactory.Stretch(w.Icon.rectTransform, 14f);
            w.ReloadFill = UIFactory.Panel("Reload", w.Background.transform, new Color(0f, 0f, 0f, 0.55f));
            UIFactory.Stretch(w.ReloadFill.rectTransform);
            w.ReloadFill.type = Image.Type.Filled;
            w.ReloadFill.fillMethod = Image.FillMethod.Vertical;
            w.ReloadFill.fillOrigin = (int)Image.OriginVertical.Top;
            w.SwapFill = UIFactory.Panel("Swap", w.Background.transform, new Color(1f, 1f, 1f, 0.35f));
            UIFactory.Stretch(w.SwapFill.rectTransform);
            w.SwapFill.type = Image.Type.Filled;
            w.SwapFill.fillMethod = Image.FillMethod.Radial360;
            w.SwapFill.fillClockwise = true;
            w.Icon.preserveAspect = true;
            // 잠긴 무기: 흑백 + 자물쇠 아이콘, 안내 문구 없음 (08 §4)
            w.Lock = UIFactory.Panel("Lock", w.Background.transform, new Color(0.95f, 0.95f, 0.95f, 0.95f));
            w.Lock.sprite = PlaceholderArt.Lock;
            w.Lock.preserveAspect = true;
            UIFactory.Place(w.Lock.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-6f, -6f), new Vector2(44f, 44f));
            w.Label = UIFactory.Label("Name", w.Background.transform, "", 20, TextAnchor.UpperCenter);
            UIFactory.Place(w.Label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(110f, 28f));
            // 강화탄 남은 무기: 아이콘 외곽선 강조 (08 §4, 09 §7)
            w.EnhancedOutline = w.Background.gameObject.AddComponent<Outline>();
            w.EnhancedOutline.effectColor = new Color(1f, 0.55f, 0.1f, 1f);
            w.EnhancedOutline.effectDistance = new Vector2(5f, -5f);
            w.EnhancedOutline.enabled = false;
            weaponWidgets[i] = w;
        }
        ammoText = UIFactory.Label("Ammo", root, "", 44, TextAnchor.LowerRight);
        UIFactory.Place(ammoText.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 190f), new Vector2(400f, 60f));

        // 하단 중앙 세트
        setsText = UIFactory.Label("Sets", root, "", 26, TextAnchor.LowerCenter);
        UIFactory.Place(setsText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(900f, 80f));

        // 라이플 스택 (08 §4): 현재 스택 + 만료까지 남은 시간 게이지. 효과 없거나 스택 0이면 숨김
        Image stackBack = UIFactory.Panel("RifleStack", root, new Color(0f, 0f, 0f, 0.5f));
        UIFactory.Place(stackBack.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 116f), new Vector2(220f, 34f));
        rifleStackFill = UIFactory.Panel("Fill", stackBack.transform, new Color(1f, 0.85f, 0.4f, 0.6f));
        UIFactory.Stretch(rifleStackFill.rectTransform, 3f);
        rifleStackFill.type = Image.Type.Filled;
        rifleStackFill.fillMethod = Image.FillMethod.Horizontal;
        rifleStackText = UIFactory.Label("Text", stackBack.transform, "", 22, TextAnchor.MiddleCenter);
        UIFactory.Stretch(rifleStackText.rectTransform);
        rifleStackRoot = stackBack.gameObject;
        rifleStackRoot.SetActive(false);

        // 상단 중앙 층 도착 안내 (10 §5.4)
        RectTransform arrival = UIFactory.CreateRect("FloorArrival", root);
        UIFactory.Place(arrival, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(800f, 120f));
        floorArrival = arrival.gameObject.AddComponent<CanvasGroup>();
        floorArrival.blocksRaycasts = false;
        floorArrivalText = UIFactory.Label("Text", arrival, "", 40, TextAnchor.UpperCenter);
        UIFactory.Stretch(floorArrivalText.rectTransform);
        floorArrival.alpha = 0f;

        // 보스 체력바 (05 §5.1 3번). 페이즈 눈금 없음
        Image bossBack = UIFactory.Panel("BossBar", root, new Color(0f, 0f, 0f, 0.7f));
        UIFactory.Place(bossBack.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(900f, 28f));
        bossFill = UIFactory.Panel("Fill", bossBack.transform, new Color(0.85f, 0.15f, 0.2f));
        UIFactory.Stretch(bossFill.rectTransform, 3f);
        bossFill.type = Image.Type.Filled;
        bossFill.fillMethod = Image.FillMethod.Horizontal;
        bossName = UIFactory.Label("Name", bossBack.transform, "", 24, TextAnchor.LowerCenter);
        UIFactory.Place(bossName.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(900f, 32f));
        bossBar = bossBack.gameObject;
        bossBar.SetActive(false);

        centerToast = UIFactory.Label("CenterToast", root, "", 48, TextAnchor.MiddleCenter, UIFactory.AccentColor);
        UIFactory.Place(centerToast.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 180f), new Vector2(1200f, 80f));
        setToast = UIFactory.Label("SetToast", root, "", 44, TextAnchor.MiddleCenter, new Color(0.6f, 1f, 0.8f));
        UIFactory.Place(setToast.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 100f), new Vector2(1200f, 70f));

        Text wait = UIFactory.Label("Wait", root, "불러오는 중...", 26, TextAnchor.LowerRight);
        UIFactory.Place(wait.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30f, 20f), new Vector2(300f, 40f));
        waitIndicator = wait.gameObject;
        waitIndicator.SetActive(false);
    }

    public void ShowFloorArrival(int floor, string name)
    {
        floorArrivalText.text = $"{floor}층\n<size=30>{name}</size>";
        floorArrivalStart = Time.unscaledTime;
        floorArrivalPausedAt = -1f;
    }

    public void ShowBossBar(FinalBoss value)
    {
        boss = value;
        bossName.text = value != null ? value.Definition.DisplayName : "";
        bossBar.SetActive(value != null);
    }

    public void ShowCenterToast(string text, float duration)
    {
        centerToast.text = text;
        centerToastUntil = Time.unscaledTime + duration;
    }

    public void QueueSetToast(string text, float duration) => setToasts.Enqueue((text, duration));

    public void SetWaitIndicator(bool show) => waitIndicator.SetActive(show);

    public override void Tick()
    {
        PlayerController player = GameServices.Player;
        if (player == null) return;

        // HP
        int maxHP = player.Health.MaxHP;
        int hp = player.Health.CurrentHP;
        UIBalance ui = GameBalance.Current.UI;
        float width = Mathf.Clamp(maxHP * ui.HPBarWidthPerHP, ui.HPBarWidthRange.x, ui.HPBarWidthRange.y);
        hpBar.sizeDelta = new Vector2(width, hpBar.sizeDelta.y);
        float ratio = maxHP > 0 ? Mathf.Clamp01(hp / (float)maxHP) : 0f;
        hpFill.fillAmount = ratio;
        hpTrail.fillAmount = hpTrail.fillAmount > ratio ? Mathf.MoveTowards(hpTrail.fillAmount, ratio, Time.unscaledDeltaTime * 0.6f) : ratio;
        hpText.text = $"{hp} / {maxHP}";

        // 회피 충전: 가득 차면 숨김, 하나라도 소모하면 표시
        int maxCharge = player.Movement.DodgeMaxCharge;
        EnsurePips(dodgePips, dodgeRow, maxCharge, new Vector2(60f, 16f), 68f);
        bool showDodge = player.Movement.DodgeCharge < maxCharge;
        dodgeRow.gameObject.SetActive(showDodge);
        for (int i = 0; i < dodgePips.Count; i++)
        {
            bool full = i < player.Movement.DodgeCharge;
            bool charging = i == player.Movement.DodgeCharge;
            dodgePips[i].color = full ? new Color(0.4f, 0.8f, 1f) : charging ? Color.Lerp(new Color(0.15f, 0.2f, 0.3f), new Color(0.4f, 0.8f, 1f), player.Movement.ChargeProgress01 * 0.6f) : new Color(0.15f, 0.2f, 0.3f);
        }

        currencyText.text = $"● {(GameServices.Wallet != null ? GameServices.Wallet.Balance : 0)}";

        // 무기
        WeaponController weapons = player.Weapons;
        for (int i = 0; i < weaponWidgets.Length && i < weapons.Slots.Count; i++)
        {
            WeaponRuntime w = weapons.Slots[i];
            WeaponSlotWidget widget = weaponWidgets[i];
            bool current = i == weapons.CurrentIndex;
            widget.Background.color = current ? new Color(0.35f, 0.35f, 0.45f, 1f) : new Color(0.12f, 0.12f, 0.15f, 0.85f);
            widget.EnhancedOutline.enabled = w.IsEnhanced;
            widget.Background.rectTransform.localScale = Vector3.one * (current ? 1.1f : 0.9f);
            widget.Icon.sprite = w.Definition.Icon != null ? w.Definition.Icon : PlaceholderArt.WeaponIcon(w.Definition.WeaponType);
            Color iconColor = w.Definition.Icon != null ? Color.white : w.Definition.PlaceholderColor;
            if (w.IsLocked) { float g = iconColor.grayscale * 0.6f; iconColor = new Color(g, g, g, iconColor.a); } // 잠긴 무기 흑백
            widget.Icon.color = iconColor;
            widget.ReloadFill.fillAmount = w.IsReloading ? 1f - w.ReloadProgress01 : 0f;   // 수직으로 차오르는 음영
            widget.SwapFill.fillAmount = current && w.IsSwapDelayed ? 1f - w.SwapDelayProgress01 : 0f; // 시계방향 음영
            widget.Lock.gameObject.SetActive(w.IsLocked);
            widget.Label.text = w.Definition.WeaponName;
        }
        // 재장전 중에도 시작 시점 잔량 유지, 완료 순간에만 갱신 (08 §4)
        WeaponRuntime cur = weapons.CurrentWeapon;
        if (cur != null) ammoText.text = $"{cur.Ammo} / {cur.MaxAmmo}";

        // 활성 세트만
        ArtifactSystem artifacts = GameServices.Artifacts;
        if (artifacts != null)
        {
            var parts = artifacts.ActiveSets().Select(s => $"<color=#{ColorUtility.ToHtmlStringRGB(s.set.Color)}>{s.set.DisplayName} {(s.tier == SetTier.Tier2 ? "Tier2" : "Tier1")}</color>");
            setsText.text = string.Join("   ", parts);
        }

        ArtifactEffectRunner effects = player.ArtifactEffects;
        int stacks = effects != null && effects.HasRifleStackEffect ? effects.RifleStacks : 0;
        rifleStackRoot.SetActive(stacks > 0);
        if (stacks > 0)
        {
            rifleStackText.text = $"라이플 스택 x{stacks}";
            rifleStackFill.fillAmount = effects.RifleStackRemaining01;
        }

        TickFloorArrival();
        TickToasts();

        // 보스 몸체가 제거되는 틱에 체력바도 사라짐 (05 §5.3 2번)
        if (bossBar.activeSelf)
        {
            if (boss == null) bossBar.SetActive(false);
            else bossFill.fillAmount = boss.MaxHP > 0 ? boss.CurrentHP / (float)boss.MaxHP : 0f;
        }

        Minimap.Tick();
    }

    // 페이드 인 / 유지 / 페이드 아웃. 일시정지와 시스템 중단 중에는 타이머 정지 (10 §5.4 6번)
    private void TickFloorArrival()
    {
        Vector3 d = GameBalance.Current.UI.FloorArrivalTextDuration;
        bool paused = GameClock.IsPausedBy(PauseReason.PauseMenu) || GameClock.IsPausedBy(PauseReason.Focus) || GameClock.IsPausedBy(PauseReason.SaveFailure);
        if (paused)
        {
            if (floorArrivalPausedAt < 0f) floorArrivalPausedAt = Time.unscaledTime;
            return;
        }
        if (floorArrivalPausedAt >= 0f)
        {
            floorArrivalStart += Time.unscaledTime - floorArrivalPausedAt;
            floorArrivalPausedAt = -1f;
        }
        float t = Time.unscaledTime - floorArrivalStart;
        float alpha;
        if (t < 0f) alpha = 0f;
        else if (t < d.x) alpha = t / Mathf.Max(0.01f, d.x);
        else if (t < d.x + d.y) alpha = 1f;
        else if (t < d.x + d.y + d.z) alpha = 1f - (t - d.x - d.y) / Mathf.Max(0.01f, d.z);
        else alpha = 0f;
        floorArrival.alpha = alpha;
    }

    private void TickToasts()
    {
        float now = Time.unscaledTime;
        centerToast.enabled = now < centerToastUntil;
        // 세트 활성 안내는 모달이 닫힌 뒤 표시 (09 §7)
        if (now >= setToastUntil && setToasts.Count > 0 && !Manager.IsModalOpen)
        {
            var next = setToasts.Dequeue();
            setToast.text = next.text;
            setToastUntil = now + next.duration;
        }
        setToast.enabled = now < setToastUntil;
    }

    private static void EnsurePips(List<Image> pips, RectTransform row, int count, Vector2 size, float spacing)
    {
        while (pips.Count < count)
        {
            Image pip = UIFactory.Panel("Pip", row, Color.white);
            UIFactory.Place(pip.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(pips.Count * spacing, 0f), size);
            pips.Add(pip);
        }
        for (int i = 0; i < pips.Count; i++) pips[i].gameObject.SetActive(i < count);
    }
}

// 08 §5.3 자동 온보딩 (비차단). 이동, 공격, 회피, 무기 전환 네 가지
public class OnboardingView : UIView
{
    public const string ControlsText =
        "이동 : W A S D\n공격 : 마우스 왼쪽 (누르고 있으면 연사)\n회피 : Space\n무기 전환 : 휠 / 1 2 3\n\n재장전 : R    상호작용 : F\n미니맵 : Tab    보유 아티팩트 : I    일시정지 : ESC";

    protected override void Build(RectTransform root)
    {
        Image panel = UIFactory.Panel("Panel", root, new Color(0f, 0f, 0f, 0.55f));
        UIFactory.Place(panel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(40f, -40f), new Vector2(520f, 260f));
        panel.raycastTarget = false;
        Text text = UIFactory.Label("Text", panel.transform, "이동 : W A S D\n공격 : 마우스 왼쪽\n회피 : Space\n무기 전환 : 휠 / 1 2 3", 32, TextAnchor.MiddleLeft);
        UIFactory.Stretch(text.rectTransform, 24f);
    }

    public override bool ClosesOnEsc => false;
    public override void OnEscape() { }
}
