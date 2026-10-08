using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// 08 §5.5 아티팩트 획득 UI: 리롤 / 유지 / 미선택. ESC는 선택 보류 (미선택 아님)
public class AcquireView : UIView
{
    private ArtifactPickup pickup;
    private RectTransform cardRoot;
    private Button reroll, keep, notSelect;
    private Text conflictText;
    public override ModalKind Kind => ModalKind.NonStop;

    protected override void Build(RectTransform root)
    {
        RectTransform panel = CenterPanel(root, new Vector2(760f, 860f));
        Title(panel, "아티팩트");
        cardRoot = UIFactory.CreateRect("Card", panel);
        UIFactory.Place(cardRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(420f, 480f));
        conflictText = UIFactory.Label("Conflict", panel, "", 24, TextAnchor.MiddleCenter, new Color(1f, 0.5f, 0.4f));
        UIFactory.Place(conflictText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 200f), new Vector2(700f, 50f));
        reroll = UIFactory.Button("Reroll", panel, "리롤", OnReroll);
        UIFactory.Place((RectTransform)reroll.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-240f, 60f), new Vector2(220f, 80f));
        keep = UIFactory.Button("Keep", panel, "유지", OnKeep);
        UIFactory.Place((RectTransform)keep.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(220f, 80f));
        notSelect = UIFactory.Button("NotSelect", panel, "미선택", OnNotSelect);
        UIFactory.Place((RectTransform)notSelect.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(240f, 60f), new Vector2(220f, 80f));
    }

    public void Setup(ArtifactPickup value)
    {
        pickup = value;
        Refresh();
    }

    // 이름, 등급, 세트, 효과를 한 화면에 표시 = 상세 정보 표시 → 도감 발견 (07 §4.7)
    private void Refresh()
    {
        UIFactory.Clear(cardRoot);
        if (pickup == null || pickup.Candidate == null) return;
        ArtifactDefinition def = pickup.Candidate;
        RectTransform card = UIFactory.ArtifactCard("Card", cardRoot, def, new Vector2(420f, 480f), true);
        UIFactory.Stretch(card);
        GameServices.Run?.Discover(def);

        conflictText.text = GameServices.Artifacts.WouldConflict(def) ? "현재 무기 특화 세트와 동시에 장착할 수 없음" : "";
        // 리롤 버튼: 처음 확인하는 세션에만, 첫 회 무료, 두 번째 비용, 다 쓰면 숨김, 재화 부족이면 회색 (07 §4.13 8번)
        reroll.gameObject.SetActive(pickup.RerollVisible);
        UIFactory.SetButtonText(reroll, pickup.NextRerollIsFree ? "리롤 - 무료" : $"리롤 - 비용 {pickup.NextRerollCost}");
        reroll.interactable = pickup.RerollEnabled;
    }

    private void OnReroll()
    {
        if (pickup != null && pickup.TryReroll()) Refresh();
    }

    private void OnKeep()
    {
        ArtifactPickup target = pickup;
        Manager.CloseView(this);
        target?.Keep();
    }

    private void OnNotSelect()
    {
        ArtifactPickup target = pickup;
        Manager.CloseView(this);
        target?.NotSelected();
    }

    public override void Tick()
    {
        if (pickup == null) { Manager.CloseView(this); return; }
        if (reroll.gameObject.activeSelf) reroll.interactable = pickup.RerollEnabled;
    }
}

// 08 §5.2 보유 아티팩트 UI: 열람 모드(I) / 획득 대기 모드(슬롯 부족, 특화 충돌 시 강제 개방)
public class InventoryView : UIView
{
    private RectTransform slotsRoot;
    private RectTransform incomingRoot;
    private Text setProgress, help;
    private ArtifactDefinition incoming;
    private AcquisitionOrigin incomingOrigin;
    private Action onEquipped, onCancelled;
    private int selectedSlot = -1;
    private bool resolved;
    public override ModalKind Kind => ModalKind.NonStop;
    public bool IsPendingMode => incoming != null;

    protected override void Build(RectTransform root)
    {
        RectTransform panel = CenterPanel(root, new Vector2(1600f, 900f));
        Title(panel, "보유 아티팩트");
        incomingRoot = UIFactory.CreateRect("Incoming", panel);
        UIFactory.Place(incomingRoot, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -100f), new Vector2(360f, 440f));
        slotsRoot = UIFactory.CreateRect("Slots", panel);
        UIFactory.Place(slotsRoot, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -100f), new Vector2(1120f, 640f));
        setProgress = UIFactory.Label("Sets", panel, "", 26, TextAnchor.UpperLeft);
        UIFactory.Place(setProgress.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, 120f), new Vector2(400f, 220f));
        help = UIFactory.Label("Help", panel, "", 24, TextAnchor.LowerCenter, new Color(0.75f, 0.75f, 0.8f));
        UIFactory.Place(help.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(1400f, 60f));
    }

    public void SetupBrowse()
    {
        incoming = null;
        onEquipped = onCancelled = null;
        resolved = false;
        Refresh();
    }

    public void SetupPending(ArtifactDefinition def, AcquisitionOrigin origin, Action equipped, Action cancelled)
    {
        incoming = def;
        incomingOrigin = origin;
        onEquipped = equipped;
        onCancelled = cancelled;
        resolved = false;
        selectedSlot = -1;
        GameServices.Run?.Discover(def);
        Refresh();
    }

    private void Refresh()
    {
        UIFactory.Clear(slotsRoot);
        UIFactory.Clear(incomingRoot);
        ArtifactSystem artifacts = GameServices.Artifacts;
        if (artifacts == null) return;

        if (incoming != null)
        {
            bool conflict = artifacts.WouldConflict(incoming);
            RectTransform card = UIFactory.ArtifactCard("Incoming", incomingRoot, incoming, new Vector2(360f, 440f), true, conflict ? "특화 충돌 - 기존 특화 아티팩트를 교체/버려야 함" : "예약됨 (장착 대기)");
            UIFactory.Stretch(card);
            help.text = "슬롯을 골라 교체하거나 버린 뒤 빈 슬롯에 장착 · ESC = 획득 취소";
        }
        else
        {
            help.text = "버리면 현재 위치 바닥에 떨어짐 · I / ESC 닫기";
        }

        for (int i = 0; i < artifacts.SlotCount; i++)
        {
            int index = i;
            ArtifactDefinition def = artifacts.Slots[i];
            int col = i % 3, row = i / 3;
            Vector2 pos = new Vector2(col * 375f, -row * 320f);
            RectTransform card = UIFactory.ArtifactCard("Slot" + i, slotsRoot, def, new Vector2(360f, 230f), def != null);
            UIFactory.Place(card, new Vector2(0f, 1f), new Vector2(0f, 1f), pos, new Vector2(360f, 230f));
            if (def == null)
            {
                Text empty = UIFactory.Label("Empty", card, "빈 슬롯", 26, TextAnchor.MiddleCenter, new Color(0.6f, 0.6f, 0.6f));
                UIFactory.Stretch(empty.rectTransform);
            }

            if (incoming != null)
            {
                if (def == null)
                {
                    Button equip = UIFactory.Button("Equip", slotsRoot, "여기에 장착", () => EquipInto(index), 22);
                    UIFactory.Place((RectTransform)equip.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), pos + new Vector2(0f, -238f), new Vector2(360f, 56f));
                    equip.interactable = artifacts.CanEquipAfter(-1, incoming, index);
                }
                else
                {
                    Button replace = UIFactory.Button("Replace", slotsRoot, "교체", () => Replace(index), 22);
                    UIFactory.Place((RectTransform)replace.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), pos + new Vector2(0f, -238f), new Vector2(175f, 56f));
                    replace.interactable = artifacts.CanEquipAfter(index, incoming, index);
                    Button drop = UIFactory.Button("Drop", slotsRoot, "버리기", () => Drop(index), 22);
                    UIFactory.Place((RectTransform)drop.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), pos + new Vector2(185f, -238f), new Vector2(175f, 56f));
                }
            }
            else if (def != null)
            {
                Button drop = UIFactory.Button("Drop", slotsRoot, "버리기", () => Drop(index), 22);
                UIFactory.Place((RectTransform)drop.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), pos + new Vector2(0f, -238f), new Vector2(360f, 56f));
            }
        }

        // 세트 진행도 "A세트 2/4" (다음 임계치)
        ArtifactBalance b = GameBalance.Current.Artifact;
        var lines = new List<string>();
        foreach (var group in artifacts.Slots.Where(s => s != null && s.Set != null).GroupBy(s => s.Set))
        {
            int count = group.Count();
            int next = count < b.Tier1Threshold ? b.Tier1Threshold : b.Tier2Threshold;
            lines.Add($"{group.Key.DisplayName} 세트 {count}/{next}");
        }
        setProgress.text = string.Join("\n", lines);
    }

    private void EquipInto(int slot)
    {
        ArtifactSystem artifacts = GameServices.Artifacts;
        if (!artifacts.CanEquipAfter(-1, incoming, slot)) return;
        artifacts.EquipToSlot(incoming, slot);
        resolved = true;
        Action callback = onEquipped;
        incoming = null;
        Manager.CloseView(this);
        callback?.Invoke();
    }

    // 교체: 기존 아티팩트는 Empty 후 안전 배치로 드랍, 새 아티팩트 장착 (07 §4.10 5번)
    private void Replace(int slot)
    {
        ArtifactSystem artifacts = GameServices.Artifacts;
        if (!artifacts.CanEquipAfter(slot, incoming, slot)) return;
        artifacts.RemoveFromSlot(slot, dropToField: true);
        EquipInto(slot);
    }

    private void Drop(int slot)
    {
        GameServices.Artifacts.RemoveFromSlot(slot, dropToField: true);
        Refresh();
    }

    // ESC: 획득 대기 취소 → 확정한 버리기 유지, 새 아티팩트는 바닥 (07 §4.10 6번)
    protected override void OnClosed()
    {
        if (incoming != null && !resolved)
        {
            Action callback = onCancelled;
            incoming = null;
            callback?.Invoke();
        }
        incoming = null;
    }
}

// 03-3 상점 UI
public class ShopView : UIView
{
    private ShopRoomController shop;
    private RectTransform slotsRoot;
    private Text currencyText;
    private readonly List<(ShopSlot slot, Button button)> buttons = new List<(ShopSlot, Button)>();
    public override ModalKind Kind => ModalKind.NonStop;

    protected override void Build(RectTransform root)
    {
        RectTransform panel = CenterPanel(root, new Vector2(1700f, 860f));
        Title(panel, "상점");
        currencyText = UIFactory.Label("Currency", panel, "", 30, TextAnchor.UpperRight, UIFactory.AccentColor);
        UIFactory.Place(currencyText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -40f), new Vector2(400f, 40f));
        slotsRoot = UIFactory.CreateRect("Slots", panel);
        UIFactory.Place(slotsRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(1620f, 600f));
        Button close = UIFactory.Button("Close", panel, "닫기", () => Manager.CloseView(this));
        UIFactory.Place((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(300f, 70f));
    }

    public void Setup(ShopRoomController value)
    {
        shop = value;
        Rebuild();
    }

    private void Rebuild()
    {
        UIFactory.Clear(slotsRoot);
        buttons.Clear();
        int count = shop.Slots.Count;
        float width = 380f, gap = 20f;
        float start = -(count - 1) * (width + gap) * 0.5f;
        for (int i = 0; i < count; i++)
        {
            ShopSlot slot = shop.Slots[i];
            Vector2 pos = new Vector2(start + i * (width + gap), 40f);
            RectTransform card;
            if (slot.Kind == ShopItemKind.Artifact && slot.Artifact != null)
            {
                card = UIFactory.ArtifactCard("Artifact", slotsRoot, slot.Artifact, new Vector2(width, 460f), true, GameServices.Artifacts.WouldConflict(slot.Artifact) ? "특화 충돌" : null);
                GameServices.Run?.Discover(slot.Artifact);
            }
            else
            {
                Image panel = UIFactory.Panel("Item", slotsRoot, UIFactory.PanelLight);
                card = panel.rectTransform;
                string label = slot.Kind switch
                {
                    ShopItemKind.SmallHeal => $"HP 회복 (소)\n+{slot.Amount} HP",
                    ShopItemKind.LargeHeal => $"HP 회복 (대)\n+{slot.Amount} HP",
                    _ => $"영구재화 묶음\n+{slot.Amount}\n(결과창 정산)",
                };
                Text text = UIFactory.Label("Text", card, label, 32, TextAnchor.MiddleCenter);
                UIFactory.Stretch(text.rectTransform, 20f);
            }
            UIFactory.Place(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(width, 460f));

            Button buy = UIFactory.Button("Buy", slotsRoot, "", () => { shop.Buy(slot); RefreshButtons(); }, 28);
            UIFactory.Place((RectTransform)buy.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos + new Vector2(0f, -275f), new Vector2(width, 70f));
            buttons.Add((slot, buy));
        }
        RefreshButtons();
    }

    // 구매 가능 상태는 재화, HP, 장착, 매진이 바뀔 때마다 다시 계산 (03-3 §8.4)
    private void RefreshButtons()
    {
        foreach ((ShopSlot slot, Button button) in buttons)
        {
            UIFactory.SetButtonText(button, slot.SoldOut ? "매진" : $"구매  ● {slot.Price}");
            button.interactable = shop.CanBuy(slot, out _);
        }
    }

    public override void Tick()
    {
        currencyText.text = $"● {(GameServices.Wallet != null ? GameServices.Wallet.Balance : 0)}";
        RefreshButtons();
    }
}

// 03-4 도박 UI: 현재 HP, 시도 비용, 시도 버튼. 당첨 뒤에는 "이미 받았다"
public class GambleView : UIView
{
    private GambleSession session;
    private GambleRoomController owner;
    private Text info, result;
    private Button attempt;
    public override ModalKind Kind => ModalKind.NonStop;

    protected override void Build(RectTransform root)
    {
        RectTransform panel = CenterPanel(root, new Vector2(900f, 600f));
        Title(panel, "도박");
        info = UIFactory.Label("Info", panel, "", 32, TextAnchor.UpperCenter);
        UIFactory.Place(info.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(820f, 160f));
        result = UIFactory.Label("Result", panel, "", 34, TextAnchor.MiddleCenter, UIFactory.AccentColor);
        UIFactory.Place(result.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(820f, 80f));
        attempt = UIFactory.Button("Attempt", panel, "시도", OnAttempt, 34);
        UIFactory.Place((RectTransform)attempt.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-170f, 40f), new Vector2(300f, 80f));
        Button close = UIFactory.Button("Close", panel, "닫기", () => Manager.CloseView(this), 30);
        UIFactory.Place((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(170f, 40f), new Vector2(300f, 80f));
    }

    public void Setup(GambleSession value, GambleRoomController room)
    {
        session = value;
        owner = room;
        if (owner != null) owner.UIOpen = true;
        result.text = session.State == GambleState.Depleted ? "이미 받았다" : "";
    }

    protected override void OnClosed()
    {
        if (owner != null) owner.UIOpen = false;
    }

    private void OnAttempt()
    {
        if (session == null) return;
        GambleSession.AttemptResult r = session.Attempt(out ArtifactDefinition won);
        switch (r)
        {
            case GambleSession.AttemptResult.Lost: result.text = "꽝!"; break;
            case GambleSession.AttemptResult.ReservationFailed: result.text = "오류: 지급할 아티팩트가 없음"; break;
            case GambleSession.AttemptResult.Won:
                result.text = $"당첨! {won.DisplayName}";
                Manager.CloseView(this);
                break;
        }
    }

    public override void Tick()
    {
        if (session == null) return;
        PlayerController player = GameServices.Player;
        info.text = session.State == GambleState.Depleted
            ? "이미 받았다"
            : $"현재 HP  {(player != null ? player.Health.CurrentHP : 0)}\n시도 비용  HP {session.Cost}";
        attempt.interactable = session.CanAttempt;
    }
}
