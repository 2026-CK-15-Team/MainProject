using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// 와이어프레임(아티팩트 UI v01) 공용 카드 부품: 이름, 세트·등급 칩, 옵션 행, 경고 행, 점선 구분선.
// 좌표는 전부 부모의 왼쪽 위 기준 (y는 아래로 음수). 패널/칩 그림은 아트가 오면 스프라이트만 교체
public static class ArtifactCardParts
{
    public static readonly Color ChipColor = new Color(0.2f, 0.2f, 0.25f, 1f);
    public static readonly Color FlavorColor = new Color(0.24f, 0.21f, 0.14f, 1f);
    public static readonly Color SelectColor = new Color(0.35f, 0.6f, 1f, 1f);

    public static RectTransform TL(RectTransform rect, Vector2 position, Vector2 size)
        => UIFactory.Place(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), position, size);

    public static Text Put(Transform parent, string name, string text, int size, Vector2 position, Vector2 box, Color? color = null, TextAnchor anchor = TextAnchor.UpperLeft)
    {
        Text label = UIFactory.Label(name, parent, text, size, anchor, color);
        TL(label.rectTransform, position, box);
        return label;
    }

    // 이름 + (◆ 세트 칩, 진행 점) + 등급 칩. 쓴 높이를 돌려줌.
    // setCount >= 0이면 세트 칩 옆에 진행 점 (장착 수 기준)
    public static float Header(RectTransform parent, ArtifactDefinition def, Vector2 position, float width, int setCount = -1, int nameSize = 34)
    {
        Color grade = ArtifactGradeUtility.Color(def.Grade);
        Put(parent, "Name", def.DisplayName, nameSize, position, new Vector2(width, nameSize + 12f), grade, TextAnchor.MiddleLeft);
        float y = position.y - nameSize - 20f;
        float x = position.x;
        string setText = def.Set != null ? "◆ " + def.Set.DisplayName : "범용";
        RectTransform setChip = UIFactory.Chip("SetChip", parent, setText, ChipColor, def.Set != null ? def.Set.Color : UIFactory.TextColor, new Vector2(x, y));
        x += setChip.sizeDelta.x + 10f;
        if (def.Set != null && setCount >= 0)
        {
            (int filled, int total) = ArtifactUIText.SetDots(setCount);
            RectTransform dots = UIFactory.Dots("SetDots", parent, filled, total, def.Set.Color, new Vector2(x, y - 11f));
            x += dots.sizeDelta.x + 8f;
        }
        UIFactory.Chip("GradeChip", parent, ArtifactGradeUtility.Display(def.Grade), new Color(grade.r * 0.3f, grade.g * 0.3f, grade.b * 0.3f, 1f), grade, new Vector2(x, y));
        return nameSize + 20f + 40f;
    }

    public static void Divider(RectTransform parent, Vector2 position, float width)
    {
        RectTransform line = UIFactory.CreateRect("Divider", parent);
        TL(line, position, new Vector2(width, 2f));
        UIFactory.Line("Dash", line, new Vector2(-width * 0.5f, 0f), new Vector2(width * 0.5f, 0f), UIFactory.LineColor, 2f, 10f, 6f);
    }

    // 줄 목록. 쓴 높이를 돌려줌
    public static float Rows(RectTransform parent, string name, IList<string> rows, Vector2 position, float width, int size, Color? color = null)
    {
        float y = position.y;
        foreach (string row in rows)
        {
            int lines = Mathf.Max(1, Mathf.CeilToInt(StripTags(row).Length * size * 0.92f / width));
            float h = lines * (size + 8f);
            Put(parent, name, row, size, new Vector2(position.x, y), new Vector2(width, h), color);
            y -= h + 4f;
        }
        return position.y - y;
    }

    // 경고 행 (빨강, ! 표시)
    public static float Warnings(RectTransform parent, IList<string> rows, Vector2 position, float width, int size = 22)
        => Rows(parent, "Warning", rows.Select(r => "! " + r).ToList(), position, width, size, ArtifactUIText.Bad);

    // 플레이버 텍스트 상자 (설명이 없으면 안 그림). 쓴 높이를 돌려줌
    public static float Flavor(RectTransform parent, ArtifactDefinition def, Vector2 position, float width, float height = 100f)
    {
        if (string.IsNullOrEmpty(def.Description)) return 0f;
        Image box = UIFactory.Panel("Flavor", parent, FlavorColor);
        TL(box.rectTransform, position, new Vector2(width, height));
        Text label = UIFactory.Label("Text", box.transform, def.Description, 22, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.7f));
        UIFactory.Stretch(label.rectTransform, 14f);
        return height;
    }

    // 큰 카드: 등급 색 프레임 + 큰 아이콘 (획득 UI 왼쪽, 도박 결과)
    public static RectTransform BigCard(RectTransform parent, ArtifactDefinition def, Vector2 position, Vector2 size)
    {
        Image card = UIFactory.Panel("Card", parent, UIFactory.PanelLight);
        TL(card.rectTransform, position, size);
        UIFactory.Frame("GradeFrame", card.transform, ArtifactGradeUtility.Color(def.Grade), 4f);
        Image icon = UIFactory.ArtifactIcon("Icon", card.transform, def);
        float s = Mathf.Min(size.x, size.y) * 0.62f;
        UIFactory.Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(s, s));
        return card.rectTransform;
    }

    // 세트 하나의 진행 칩 "◆ ●●○○" (bar용). 쓴 폭을 돌려줌
    public static float SetProgressChip(RectTransform parent, ArtifactSetDefinition set, int count, Vector2 position, int afterCount = -1)
    {
        Image diamond = UIFactory.Panel("SetIcon", parent, set.Color);
        diamond.sprite = PlaceholderArt.Diamond;
        diamond.raycastTarget = false;
        TL(diamond.rectTransform, position + new Vector2(0f, -6f), new Vector2(24f, 24f));
        float x = position.x + 30f;
        (int filled, int total) = ArtifactUIText.SetDots(count);
        RectTransform dots = UIFactory.Dots("Dots", parent, filled, total, set.Color, new Vector2(x, position.y - 9f));
        x += dots.sizeDelta.x;
        if (afterCount >= 0 && afterCount != count)
        {
            Put(parent, "Arrow", "→", 22, new Vector2(x, position.y - 2f), new Vector2(28f, 32f), ArtifactUIText.Muted, TextAnchor.MiddleCenter);
            x += 30f;
            (int f2, int t2) = ArtifactUIText.SetDots(afterCount);
            RectTransform after = UIFactory.Dots("DotsAfter", parent, f2, t2, afterCount > count ? ArtifactUIText.Good : ArtifactUIText.Bad, new Vector2(x, position.y - 9f));
            x += after.sizeDelta.x;
        }
        return x - position.x + 22f;
    }

    private static string StripTags(string s) => System.Text.RegularExpressions.Regex.Replace(s, "<.*?>", "");
}

// 08 §5.5 아티팩트 획득 UI (와이어프레임 1): 왼쪽 큰 카드, 오른쪽 이름·칩·옵션·플레이버·경고, 아래 리롤/유지/미선택.
// X·ESC = 보류 (후보와 리롤 상태 유지). 미선택도 리롤 상태 유지 (기획 확정: 07 §4.8 6번 대체)
public class AcquireView : UIView
{
    private static readonly Vector2 PanelSize = new Vector2(1180f, 720f);
    private ArtifactPickup pickup;
    private RectTransform panel, content;
    private Button reroll, keep, notSelect;
    public override ModalKind Kind => ModalKind.NonStop;

    protected override void Build(RectTransform root)
    {
        panel = CenterPanel(root, PanelSize);
        UIFactory.Tab(panel, "아티팩트 획득");
        Button close = UIFactory.Button("Close", panel, "X", () => Manager.CloseView(this), UIFactory.SecondaryColor, 30);
        UIFactory.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -20f), new Vector2(60f, 60f));
        content = UIFactory.CreateRect("Content", panel);
        UIFactory.Stretch(content);

        // 버튼 자리는 고정. 리롤을 다 쓰면 숨기고 자리는 비워 둠
        float w = (PanelSize.x - 80f - 2f * 40f) / 3f;
        reroll = UIFactory.Button("Reroll", panel, "리롤", OnReroll, UIFactory.RerollColor);
        UIFactory.Place((RectTransform)reroll.transform, Vector2.zero, Vector2.zero, new Vector2(40f, 40f), new Vector2(w, 80f));
        keep = UIFactory.Button("Keep", panel, "유지", OnKeep, UIFactory.PrimaryColor);
        UIFactory.Place((RectTransform)keep.transform, Vector2.zero, Vector2.zero, new Vector2(40f + w + 40f, 40f), new Vector2(w, 80f));
        notSelect = UIFactory.Button("NotSelect", panel, "미선택", OnNotSelect, UIFactory.SecondaryColor);
        UIFactory.Place((RectTransform)notSelect.transform, Vector2.zero, Vector2.zero, new Vector2(40f + 2f * (w + 40f), 40f), new Vector2(w, 80f));
    }

    public void Setup(ArtifactPickup value)
    {
        pickup = value;
        Refresh();
    }

    // 이름, 등급, 세트, 효과를 한 화면에 표시 = 상세 정보 표시 → 도감 발견 (07 §4.7)
    private void Refresh()
    {
        UIFactory.Clear(content);
        if (pickup == null || pickup.Candidate == null) return;
        ArtifactDefinition def = pickup.Candidate;
        GameServices.Run?.Discover(def);

        ArtifactCardParts.BigCard(content, def, new Vector2(40f, -70f), new Vector2(340f, 470f));

        const float x = 420f;
        float width = PanelSize.x - x - 40f;
        float y = -70f;
        int setCount = def.Set != null ? GameServices.Artifacts.SetCount(def.Set) + 1 : -1;
        y -= ArtifactCardParts.Header(content, def, new Vector2(x, y), width - 80f, setCount) + 16f;
        ArtifactCardParts.Divider(content, new Vector2(x, y), width);
        y -= 18f;
        y -= ArtifactCardParts.Rows(content, "Option", ArtifactUIText.OptionRows(def, false), new Vector2(x, y), width, 26) + 12f;
        float flavor = ArtifactCardParts.Flavor(content, def, new Vector2(x, y), width);
        if (flavor > 0f) y -= flavor + 12f;
        ArtifactCardParts.Warnings(content, ArtifactUIText.WarningRows(def), new Vector2(x, y), width);

        // 리롤 버튼: 첫 회 무료, 두 번째 비용, 다 쓰면 숨김, 재화 부족이면 회색 (07 §4.13 8번)
        reroll.gameObject.SetActive(pickup.RerollVisible);
        RefreshReroll();
    }

    // 후보가 없어 못 누르면 이유를 버튼에 표시
    private void RefreshReroll()
    {
        string label = !pickup.RerollHasCandidate ? "리롤 - 후보 없음"
            : pickup.NextRerollIsFree ? "리롤 - 무료" : $"리롤 - ● {pickup.NextRerollCost}";
        UIFactory.SetButtonText(reroll, label);
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
        if (reroll.gameObject.activeSelf) RefreshReroll();
    }
}

// 08 §5.2 보유 아티팩트 UI (와이어프레임 3-a, 3-b)
// 열람 모드(I): 6칸 링 + 선택 칸 상세 + 세트 진행. 버리기 확인창 없음 (바닥에서 다시 주울 수 있음)
// 획득 대기 모드(슬롯 부족, 특화 충돌 시 강제 개방): 링 가운데 NEW 예약 칸, 선택 칸과 나란히 비교, 교체하면 세트 변화 요약
public class InventoryView : UIView
{
    private static readonly Vector2 PanelSize = new Vector2(1560f, 880f);
    private const float RingRadius = 240f;
    private static readonly Vector2 SlotSize = new Vector2(150f, 150f);

    private RectTransform panel, ring, detail, bar;
    private Text tab, hint;
    private Button close, drop, secondary;
    private ArtifactDefinition incoming;
    private AcquisitionOrigin incomingOrigin;
    private Action onEquipped, onCancelled;
    private int selectedSlot = -1;
    private bool resolved;
    public override ModalKind Kind => ModalKind.NonStop;
    public bool IsPendingMode => incoming != null;

    protected override void Build(RectTransform root)
    {
        panel = CenterPanel(root, PanelSize);
        tab = UIFactory.Tab(panel, "보유 아티팩트", 60f, 0f);
        hint = UIFactory.Label("Hint", panel, "", 22, TextAnchor.MiddleRight, ArtifactUIText.Muted);
        UIFactory.Place(hint.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-100f, -26f), new Vector2(700f, 50f));
        close = UIFactory.Button("X", panel, "X", () => Manager.CloseView(this), UIFactory.SecondaryColor, 30);
        UIFactory.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -20f), new Vector2(60f, 60f));

        ring = UIFactory.CreateRect("Ring", panel);
        ArtifactCardParts.TL(ring, new Vector2(60f, -100f), new Vector2(640f, 640f));
        detail = UIFactory.CreateRect("Detail", panel);
        ArtifactCardParts.TL(detail, new Vector2(760f, -90f), new Vector2(760f, 640f));

        Image barPanel = UIFactory.Panel("SetBar", panel, UIFactory.PanelLight);
        UIFactory.Place(barPanel.rectTransform, Vector2.zero, Vector2.zero, new Vector2(40f, 30f), new Vector2(680f, 80f));
        bar = barPanel.rectTransform;

        // 버튼 순서는 와이어프레임 그대로: 버리기(주) 왼쪽, 닫기/교체(보조) 오른쪽
        drop = UIFactory.Button("Drop", panel, "버리기", OnDrop, UIFactory.PrimaryColor);
        UIFactory.Place((RectTransform)drop.transform, Vector2.zero, Vector2.zero, new Vector2(760f, 30f), new Vector2(360f, 80f));
        secondary = UIFactory.Button("Secondary", panel, "닫기", OnSecondary, UIFactory.SecondaryColor);
        UIFactory.Place((RectTransform)secondary.transform, Vector2.zero, Vector2.zero, new Vector2(1160f, 30f), new Vector2(360f, 80f));
    }

    public void SetupBrowse()
    {
        incoming = null;
        onEquipped = onCancelled = null;
        resolved = false;
        selectedSlot = FirstEquipped();
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

    private int FirstEquipped()
    {
        ArtifactSystem artifacts = GameServices.Artifacts;
        if (artifacts == null) return -1;
        for (int i = 0; i < artifacts.SlotCount; i++)
            if (artifacts.Slots[i] != null) return i;
        return -1;
    }

    // 링 위치: 1번 위, 시계 방향
    private static Vector2 SlotPosition(int index, int count)
    {
        float angle = (90f - index * 360f / count) * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * RingRadius;
    }

    // 획득 대기 모드에서 이 칸이 잠겼는지 (특화 위반)
    private bool IsLockedSlot(int index)
    {
        ArtifactSystem artifacts = GameServices.Artifacts;
        if (incoming == null) return false;
        return artifacts.Slots[index] != null
            ? !artifacts.CanEquipAfter(index, incoming, index)
            : !artifacts.CanEquipAfter(-1, incoming, index);
    }

    private void Refresh()
    {
        UIFactory.Clear(ring);
        UIFactory.Clear(detail);
        UIFactory.Clear(bar);
        ArtifactSystem artifacts = GameServices.Artifacts;
        if (artifacts == null) return;

        bool pending = incoming != null;
        tab.text = pending ? "아티팩트 정리" : "보유 아티팩트";
        hint.text = pending ? "ESC: 취소 (새 아티팩트는 바닥에 놓임)" : "";
        close.gameObject.SetActive(pending);

        BuildRing(artifacts);
        if (pending) BuildPendingDetail(artifacts);
        else BuildBrowseDetail(artifacts);
        BuildSetBar(artifacts);
        RefreshButtons(artifacts);
    }

    private void BuildRing(ArtifactSystem artifacts)
    {
        int count = artifacts.SlotCount;
        // 기본 링 점선
        for (int i = 0; i < count; i++)
            UIFactory.Line("RingLine", ring, SlotPosition(i, count), SlotPosition((i + 1) % count, count), UIFactory.LineColor, 2f, 10f, 8f);
        // 같은 세트끼리 굵은 점선 (세트 색)
        for (int i = 0; i < count; i++)
        {
            for (int j = i + 1; j < count; j++)
            {
                ArtifactDefinition a = artifacts.Slots[i], b = artifacts.Slots[j];
                if (a == null || b == null || a.Set == null || a.Set != b.Set) continue;
                UIFactory.Line("SetLine", ring, SlotPosition(i, count), SlotPosition(j, count), a.Set.Color, 6f, 16f, 8f);
            }
        }

        for (int i = 0; i < count; i++)
        {
            int index = i;
            ArtifactDefinition def = artifacts.Slots[i];
            bool locked = IsLockedSlot(i);
            Button slot = UIFactory.Button("Slot" + i, ring, "", () => Select(index), def != null ? UIFactory.PanelLight : new Color(0.12f, 0.12f, 0.15f, 1f));
            UIFactory.Place((RectTransform)slot.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), SlotPosition(i, count), SlotSize);
            Transform t = slot.transform;

            if (def != null)
            {
                UIFactory.Frame("GradeFrame", t, ArtifactGradeUtility.Color(def.Grade), 3f);
                Image icon = UIFactory.ArtifactIcon("Icon", t, def);
                UIFactory.Place(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(76f, 76f));
            }
            string label = def != null ? $"{i + 1} {def.DisplayName}" : "빈 칸";
            Text text = UIFactory.Label("Label", t, label, 20, TextAnchor.LowerCenter, def != null ? UIFactory.TextColor : ArtifactUIText.Muted);
            UIFactory.Stretch(text.rectTransform, 8f);

            if (locked)
            {
                Image dim = UIFactory.Panel("Locked", t, new Color(0.3f, 0.3f, 0.3f, 0.75f));
                UIFactory.Stretch(dim.rectTransform);
                dim.raycastTarget = false;
                Image lockIcon = UIFactory.Panel("Lock", t, Color.white);
                lockIcon.sprite = PlaceholderArt.Lock;
                lockIcon.raycastTarget = false;
                UIFactory.Place(lockIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(48f, 48f));
            }
            if (i == selectedSlot) UIFactory.Frame("Selected", t, ArtifactCardParts.SelectColor, 6f);
        }

        // 예약 칸: 링 가운데 새 아티팩트
        if (incoming != null)
        {
            Image reserved = UIFactory.Panel("Reserved", ring, ArtifactCardParts.FlavorColor);
            UIFactory.Place(reserved.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, SlotSize);
            Transform t = reserved.transform;
            UIFactory.Frame("Frame", t, UIFactory.AccentColor, 3f);
            Image icon = UIFactory.ArtifactIcon("Icon", t, incoming);
            UIFactory.Place(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(76f, 76f));
            Text text = UIFactory.Label("Label", t, "NEW", 22, TextAnchor.LowerCenter, UIFactory.AccentColor);
            UIFactory.Stretch(text.rectTransform, 8f);
        }
    }

    // 열람 모드 상세: 아이콘 + 이름·칩 + 옵션 행(실효값) + 세트 진행 (Tier 줄)
    private void BuildBrowseDetail(ArtifactSystem artifacts)
    {
        ArtifactDefinition def = selectedSlot >= 0 ? artifacts.Slots[selectedSlot] : null;
        const float width = 760f;
        if (def == null)
        {
            ArtifactCardParts.Put(detail, "Empty", artifacts.EquippedCount == 0 ? "장착한 아티팩트 없음" : "칸을 선택하세요", 26, new Vector2(0f, -20f), new Vector2(width, 40f), ArtifactUIText.Muted);
            return;
        }

        Image circle = UIFactory.Panel("IconBack", detail, ArtifactCardParts.ChipColor);
        circle.sprite = PlaceholderArt.Circle;
        ArtifactCardParts.TL(circle.rectTransform, new Vector2(0f, 0f), new Vector2(120f, 120f));
        Image icon = UIFactory.ArtifactIcon("Icon", circle.transform, def);
        UIFactory.Stretch(icon.rectTransform, 22f);

        ArtifactCardParts.Header(detail, def, new Vector2(140f, -6f), width - 140f);
        float y = -140f;
        ArtifactCardParts.Divider(detail, new Vector2(0f, y), width);
        y -= 16f;
        y -= ArtifactCardParts.Rows(detail, "Option", ArtifactUIText.OptionRows(def, true), new Vector2(0f, y), width, 24) + 16f;

        if (def.Set == null) return;
        ArtifactSetDefinition set = def.Set;
        int count = artifacts.SetCount(set);
        ArtifactBalance b = GameBalance.Current.Artifact;
        Image head = UIFactory.Panel("SetHead", detail, ArtifactCardParts.FlavorColor);
        ArtifactCardParts.TL(head.rectTransform, new Vector2(0f, y), new Vector2(width, 40f));
        ArtifactCardParts.Put(head.transform, "Title", $"세트 진행 · {set.DisplayName}", 22, new Vector2(12f, 0f), new Vector2(width - 24f, 40f), UIFactory.AccentColor, TextAnchor.MiddleLeft);
        y -= 48f;
        (int filled, int total) = ArtifactUIText.SetDots(count);
        UIFactory.Dots("Dots", detail, filled, total, set.Color, new Vector2(12f, y - 8f), 22f, 8f);
        ArtifactCardParts.Put(detail, "Count", $"{count}/{total}", 22, new Vector2(30f + total * 30f, y), new Vector2(100f, 36f), null, TextAnchor.MiddleLeft);
        y -= 44f;
        y -= TierLine(set, SetTier.Tier1, count, b.Tier1Threshold, new Vector2(12f, y), width - 24f);
        TierLine(set, SetTier.Tier2, count, b.Tier2Threshold, new Vector2(12f, y), width - 24f);
    }

    private float TierLine(ArtifactSetDefinition set, SetTier tier, int count, int threshold, Vector2 position, float width)
    {
        bool on = count >= threshold;
        string name = ArtifactUIText.TierName(tier);
        string text = on ? $"✓ {name} 켜짐: {set.TierText(tier)}" : $"잠김 {name} ({threshold - count}개 더 필요): {set.TierText(tier)}";
        return ArtifactCardParts.Rows(detail, "Tier", new[] { text }, position, width, 22, on ? ArtifactUIText.Good : ArtifactUIText.Muted) + 6f;
    }

    // 획득 대기 상세: 선택 칸과 새 아티팩트 나란히 + 교체하면 요약
    private void BuildPendingDetail(ArtifactSystem artifacts)
    {
        const float width = 760f;
        const float cardW = 370f, cardH = 320f;
        ArtifactDefinition current = selectedSlot >= 0 ? artifacts.Slots[selectedSlot] : null;
        string title = selectedSlot < 0 ? "칸을 선택하세요" : $"선택 슬롯 {selectedSlot + 1}와 새 아티팩트";
        ArtifactCardParts.Put(detail, "Title", title, 26, new Vector2(0f, 0f), new Vector2(width, 40f));

        CompareCard(new Vector2(0f, -50f), new Vector2(cardW, cardH), "장착 중", current, true, false);
        CompareCard(new Vector2(width - cardW, -50f), new Vector2(cardW, cardH), "새 아티팩트", incoming, false, true);

        float y = -50f - cardH - 20f;
        Image head = UIFactory.Panel("ChangeHead", detail, ArtifactCardParts.FlavorColor);
        ArtifactCardParts.TL(head.rectTransform, new Vector2(0f, y), new Vector2(width, 40f));
        ArtifactCardParts.Put(head.transform, "Title", "교체하면", 22, new Vector2(12f, 0f), new Vector2(width - 24f, 40f), UIFactory.AccentColor, TextAnchor.MiddleLeft);
        y -= 50f;

        var lines = new List<string>();
        if (selectedSlot >= 0)
        {
            if (IsLockedSlot(selectedSlot))
                lines.Add($"<color=#{ArtifactUIText.Hex(ArtifactUIText.Bad)}>잠김: 이 칸에는 넣을 수 없음 (특화 위반)</color>");
            foreach ((string text, bool good) in ArtifactUIText.ChangeLines(artifacts.Slots.ToList(), artifacts.LoadoutWith(selectedSlot, incoming)))
                lines.Add(good ? $"<color=#{ArtifactUIText.Hex(ArtifactUIText.Good)}>✓ {text}</color>" : $"<color=#{ArtifactUIText.Hex(ArtifactUIText.Bad)}>{text}</color>");
            if (lines.Count == 0) lines.Add($"<color=#{ArtifactUIText.Hex(ArtifactUIText.Muted)}>세트 변화 없음</color>");
        }
        ArtifactCardParts.Rows(detail, "Change", lines, new Vector2(12f, y), width - 24f, 22);
    }

    private void CompareCard(Vector2 position, Vector2 size, string label, ArtifactDefinition def, bool equipped, bool highlight)
    {
        Image card = UIFactory.Panel(label, detail, highlight ? ArtifactCardParts.FlavorColor : UIFactory.PanelLight);
        RectTransform rect = ArtifactCardParts.TL(card.rectTransform, position, size);
        UIFactory.Frame("Frame", rect, highlight ? UIFactory.AccentColor : UIFactory.LineColor, 2f);
        ArtifactCardParts.Put(rect, "Label", label, 20, new Vector2(16f, -10f), new Vector2(size.x - 32f, 28f), ArtifactUIText.Muted);
        if (def == null)
        {
            ArtifactCardParts.Put(rect, "Empty", "빈 칸", 24, new Vector2(16f, -50f), new Vector2(size.x - 32f, 40f), ArtifactUIText.Muted);
            return;
        }
        float y = -44f;
        y -= ArtifactCardParts.Header(rect, def, new Vector2(16f, y), size.x - 32f, -1, 26) + 10f;
        ArtifactCardParts.Rows(rect, "Option", ArtifactUIText.OptionRows(def, equipped), new Vector2(16f, y), size.x - 32f, 19);
    }

    // 아래 막대: 활성 세트 진행. 대기 모드에서 칸을 고르면 "현재 → 교체 후"
    private void BuildSetBar(ArtifactSystem artifacts)
    {
        List<ArtifactDefinition> before = artifacts.Slots.ToList();
        List<ArtifactDefinition> after = incoming != null && selectedSlot >= 0 ? artifacts.LoadoutWith(selectedSlot, incoming) : null;
        var sets = before.Concat(after ?? new List<ArtifactDefinition>()).Where(a => a != null && a.Set != null).Select(a => a.Set).Distinct().ToList();
        float x = 20f;
        if (sets.Count == 0)
        {
            ArtifactCardParts.Put(bar, "None", "활성 세트 없음", 22, new Vector2(20f, -22f), new Vector2(400f, 36f), ArtifactUIText.Muted, TextAnchor.MiddleLeft);
            return;
        }
        foreach (ArtifactSetDefinition set in sets)
        {
            int now = before.Count(a => a != null && a.Set == set);
            int next = after != null ? after.Count(a => a != null && a.Set == set) : -1;
            x += ArtifactCardParts.SetProgressChip(bar, set, now, new Vector2(x, -22f), next);
        }
        if (after != null)
            ArtifactCardParts.Put(bar, "Legend", "현재 → 교체 후", 18, new Vector2(x, -24f), new Vector2(Mathf.Max(0f, 670f - x), 32f), ArtifactUIText.Muted, TextAnchor.MiddleRight);
    }

    private void RefreshButtons(ArtifactSystem artifacts)
    {
        ArtifactDefinition current = selectedSlot >= 0 ? artifacts.Slots[selectedSlot] : null;
        drop.interactable = current != null;
        if (incoming == null)
        {
            UIFactory.SetButtonText(secondary, "닫기");
            secondary.interactable = true;
            return;
        }
        // 버리기 뒤(빈 칸 선택)에는 보조 버튼이 "이 칸에 장착"
        UIFactory.SetButtonText(secondary, current != null ? "이 슬롯과 교체" : "이 칸에 장착");
        secondary.interactable = selectedSlot >= 0 && !IsLockedSlot(selectedSlot);
    }

    private void Select(int slot)
    {
        selectedSlot = slot;
        Refresh();
    }

    private void OnSecondary()
    {
        if (incoming == null) { Manager.CloseView(this); return; }
        if (selectedSlot < 0) return;
        if (GameServices.Artifacts.Slots[selectedSlot] != null) Replace(selectedSlot);
        else EquipInto(selectedSlot);
    }

    private void OnDrop()
    {
        if (selectedSlot < 0 || GameServices.Artifacts.Slots[selectedSlot] == null) return;
        GameServices.Artifacts.RemoveFromSlot(selectedSlot, dropToField: true);
        if (incoming == null) selectedSlot = FirstEquipped();
        Refresh();
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

// 03-3 상점 UI (와이어프레임 2): 상품 목록에서 하나 선택 → 가격 → 구매/닫기
public class ShopView : UIView
{
    private static readonly Vector2 PanelSize = new Vector2(640f, 860f);
    private const float RowHeight = 124f, RowGap = 14f;
    private ShopRoomController shop;
    private RectTransform list;
    private Text price, reason;
    private Button buy;
    private int selected = -1;
    public override ModalKind Kind => ModalKind.NonStop;

    protected override void Build(RectTransform root)
    {
        RectTransform panel = CenterPanel(root, PanelSize);
        UIFactory.Tab(panel, "상점");
        list = UIFactory.CreateRect("List", panel);
        ArtifactCardParts.TL(list, new Vector2(40f, -60f), new Vector2(PanelSize.x - 80f, 4f * (RowHeight + RowGap)));
        price = UIFactory.Label("Price", panel, "", 28, TextAnchor.MiddleLeft);
        UIFactory.Place(price.rectTransform, Vector2.zero, Vector2.zero, new Vector2(40f, 170f), new Vector2(PanelSize.x - 80f, 44f));
        reason = UIFactory.Label("Reason", panel, "", 22, TextAnchor.MiddleLeft, ArtifactUIText.Bad);
        UIFactory.Place(reason.rectTransform, Vector2.zero, Vector2.zero, new Vector2(40f, 130f), new Vector2(PanelSize.x - 80f, 36f));
        float w = (PanelSize.x - 80f - 30f) / 2f;
        buy = UIFactory.Button("Buy", panel, "구매", OnBuy, UIFactory.PrimaryColor);
        UIFactory.Place((RectTransform)buy.transform, Vector2.zero, Vector2.zero, new Vector2(40f, 36f), new Vector2(w, 76f));
        Button close = UIFactory.Button("Close", panel, "닫기", () => Manager.CloseView(this), UIFactory.SecondaryColor);
        UIFactory.Place((RectTransform)close.transform, Vector2.zero, Vector2.zero, new Vector2(70f + w, 36f), new Vector2(w, 76f));
    }

    public void Setup(ShopRoomController value)
    {
        shop = value;
        selected = FirstAvailable();
        Rebuild();
    }

    private int FirstAvailable()
    {
        for (int i = 0; i < shop.Slots.Count; i++)
            if (!shop.Slots[i].SoldOut) return i;
        return shop.Slots.Count > 0 ? 0 : -1;
    }

    private void Rebuild()
    {
        UIFactory.Clear(list);
        float width = PanelSize.x - 80f;
        for (int i = 0; i < shop.Slots.Count; i++)
        {
            int index = i;
            ShopSlot slot = shop.Slots[i];
            bool isSelected = i == selected;
            Color back = isSelected ? new Color(0.22f, 0.25f, 0.34f, 1f) : UIFactory.PanelLight;
            Button row = UIFactory.Button("Row" + i, list, "", () => { selected = index; Rebuild(); }, back);
            RectTransform rect = ArtifactCardParts.TL((RectTransform)row.transform, new Vector2(0f, -i * (RowHeight + RowGap)), new Vector2(width, RowHeight));
            if (isSelected) UIFactory.Frame("Selected", rect, ArtifactCardParts.SelectColor, 3f);

            Image icon = RowIcon(rect, slot);
            ArtifactCardParts.TL(icon.rectTransform, new Vector2(20f, -22f), new Vector2(80f, 80f));
            (string name, Color nameColor, string desc) = Describe(slot);
            ArtifactCardParts.Put(rect, "Name", name, 26, new Vector2(120f, -14f), new Vector2(width - 140f, 36f), nameColor);
            Text descText = ArtifactCardParts.Put(rect, "Desc", desc, 18, new Vector2(120f, -52f), new Vector2(width - 140f, 66f), ArtifactUIText.Muted);
            descText.verticalOverflow = VerticalWrapMode.Truncate;
            if (slot.SoldOut)
            {
                Image dim = UIFactory.Panel("SoldOut", rect, new Color(0f, 0f, 0f, 0.55f));
                UIFactory.Stretch(dim.rectTransform);
                dim.raycastTarget = false;
            }
            if (slot.Kind == ShopItemKind.Artifact && slot.Artifact != null) GameServices.Run?.Discover(slot.Artifact);
        }
        RefreshPrice();
    }

    private static Image RowIcon(RectTransform row, ShopSlot slot)
    {
        if (slot.Kind == ShopItemKind.Artifact && slot.Artifact != null) return UIFactory.ArtifactIcon("Icon", row, slot.Artifact);
        Image icon = UIFactory.Panel("Icon", row, slot.Kind == ShopItemKind.PermanentBundle ? UIFactory.AccentColor : new Color(1f, 0.35f, 0.4f));
        icon.sprite = slot.Kind == ShopItemKind.PermanentBundle ? PlaceholderArt.Coin : PlaceholderArt.Heart;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        return icon;
    }

    private static (string, Color, string) Describe(ShopSlot slot)
    {
        if (slot.SoldOut) return ("매진", ArtifactUIText.Muted, "");
        switch (slot.Kind)
        {
            case ShopItemKind.Artifact when slot.Artifact != null:
                ArtifactDefinition def = slot.Artifact;
                string set = def.Set != null ? def.Set.DisplayName + " 세트" : "범용";
                string desc = $"{ArtifactGradeUtility.Display(def.Grade)} · {set} · {string.Join(", ", ArtifactUIText.PlainRows(def))}";
                if (GameServices.Artifacts.WouldConflict(def)) desc = $"<color=#{ArtifactUIText.Hex(ArtifactUIText.Bad)}>특화 충돌</color> · " + desc;
                return (def.DisplayName, ArtifactGradeUtility.Color(def.Grade), desc);
            case ShopItemKind.SmallHeal: return ("HP 회복 (소)", UIFactory.TextColor, $"HP +{slot.Amount} 즉시 회복");
            case ShopItemKind.LargeHeal: return ("HP 회복 (대)", UIFactory.TextColor, $"HP +{slot.Amount} 즉시 회복");
            default: return ("영구재화 묶음", UIFactory.AccentColor, $"영구재화 +{slot.Amount} (결과창 정산)");
        }
    }

    // 구매 가능 상태는 재화, HP, 장착, 매진이 바뀔 때마다 다시 계산 (03-3 §8.4)
    private void RefreshPrice()
    {
        int balance = GameServices.Wallet != null ? GameServices.Wallet.Balance : 0;
        ShopSlot slot = selected >= 0 && selected < shop.Slots.Count ? shop.Slots[selected] : null;
        if (slot == null)
        {
            price.text = $"보유 ● {balance}";
            reason.text = "";
            buy.interactable = false;
            return;
        }
        price.text = slot.SoldOut ? $"매진   <color=#{ArtifactUIText.Hex(ArtifactUIText.Muted)}>(보유 ● {balance})</color>" : $"가격 ● {slot.Price}   <color=#{ArtifactUIText.Hex(ArtifactUIText.Muted)}>(보유 ● {balance})</color>";
        bool can = shop.CanBuy(slot, out string why);
        reason.text = can || slot.SoldOut ? "" : why;
        buy.interactable = can;
    }

    private void OnBuy()
    {
        if (selected < 0 || selected >= shop.Slots.Count) return;
        // 슬롯이 가득 차거나 특화 충돌이면 GrantImmediate가 상점을 닫고 획득 대기 모드를 엶
        if (!shop.Buy(shop.Slots[selected]) || !IsOpen) return;
        selected = FirstAvailable();
        Rebuild();
    }

    public override void Tick() => RefreshPrice();
}

// 03-4 도박 UI (와이어프레임 4): 결과 카드 자리(시도 전 뒷면 ?) + HP 게이지(소모될 체력 표시) + 시도/닫기.
// 공개 중 버튼 잠금, 승리·near-miss 연출 없음. 당첨 충돌은 결과 카드에서 처음 표시, 지급은 공개 뒤
public class GambleView : UIView
{
    private enum Face { Back, Lost, Won, Depleted }
    private const float FlipTime = 0.18f;   // 반쪽 뒤집기 시간
    private const float WonHold = 0.8f;     // 당첨 카드 보여 준 뒤 지급까지

    private static readonly Vector2 PanelSize = new Vector2(580f, 860f);
    private static readonly Vector2 CardSize = new Vector2(440f, 440f);
    private GambleSession session;
    private GambleRoomController owner;
    private RectTransform card;
    private Text chance, costText;
    private Button attempt;
    private Face face, pendingFace;
    private ArtifactDefinition shownArtifact;
    private float revealTimer = -1f;
    public override ModalKind Kind => ModalKind.NonStop;
    private bool Revealing => revealTimer >= 0f;

    protected override void Build(RectTransform root)
    {
        RectTransform panel = CenterPanel(root, PanelSize);
        UIFactory.Tab(panel, "도박");
        RectTransform slotFrame = UIFactory.CreateRect("CardSlot", panel);
        ArtifactCardParts.TL(slotFrame, new Vector2((PanelSize.x - CardSize.x) * 0.5f - 12f, -58f), CardSize + new Vector2(24f, 24f));
        Vector2 h = (CardSize + new Vector2(24f, 24f)) * 0.5f;
        Vector2[] corners = { new Vector2(-h.x, -h.y), new Vector2(h.x, -h.y), new Vector2(h.x, h.y), new Vector2(-h.x, h.y) };
        for (int i = 0; i < 4; i++) UIFactory.Line("Dash", slotFrame, corners[i], corners[(i + 1) % 4], UIFactory.LineColor, 2f, 10f, 6f);

        card = UIFactory.CreateRect("Card", panel);
        ArtifactCardParts.TL(card, new Vector2((PanelSize.x - CardSize.x) * 0.5f, -70f), CardSize);
        card.pivot = new Vector2(0.5f, 1f);
        card.anchoredPosition = new Vector2(PanelSize.x * 0.5f, -70f);

        chance = UIFactory.Label("Chance", panel, "", 22, TextAnchor.MiddleCenter, ArtifactUIText.Muted);
        UIFactory.Place(chance.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -540f), new Vector2(PanelSize.x - 60f, 36f));

        Image costBack = UIFactory.Panel("Cost", panel, UIFactory.PanelLight);
        UIFactory.Place(costBack.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -590f), new Vector2(CardSize.x, 64f));
        costText = UIFactory.Label("CostText", costBack.transform, "", 26, TextAnchor.MiddleCenter);
        UIFactory.Stretch(costText.rectTransform);

        float w = (CardSize.x - 20f) / 2f;
        attempt = UIFactory.Button("Attempt", panel, "시도한다", OnAttempt, UIFactory.PrimaryColor);
        UIFactory.Place((RectTransform)attempt.transform, new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(-10f, 36f), new Vector2(w, 76f));
        Button close = UIFactory.Button("Close", panel, "닫기", () => Manager.CloseView(this), UIFactory.SecondaryColor);
        UIFactory.Place((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(10f, 36f), new Vector2(w, 76f));
    }

    public void Setup(GambleSession value, GambleRoomController room)
    {
        session = value;
        owner = room;
        if (owner != null) owner.UIOpen = true;
        revealTimer = -1f;
        shownArtifact = null;
        ShowFace(session.State == GambleState.Depleted ? Face.Depleted : Face.Back);
    }

    // 닫으면 공개 중이던 당첨도 바로 지급 (연출만 건너뜀)
    protected override void OnClosed()
    {
        if (owner != null) owner.UIOpen = false;
        revealTimer = -1f;
        session?.Deliver();
    }

    private void OnAttempt()
    {
        if (session == null || Revealing) return;
        GambleSession.AttemptResult r = session.Attempt(out ArtifactDefinition won, deferDelivery: true);
        switch (r)
        {
            case GambleSession.AttemptResult.Lost: StartReveal(Face.Lost, null); break;
            case GambleSession.AttemptResult.Won: StartReveal(Face.Won, won); break;
            case GambleSession.AttemptResult.ReservationFailed: GameServices.UI?.ShowCenterToast("지급할 아티팩트가 없음", 1f); break;
        }
    }

    // 이미 앞면이면 뒷면으로 접었다가 결과로 펼침
    private void StartReveal(Face result, ArtifactDefinition won)
    {
        pendingFace = result;
        shownArtifact = won;
        revealTimer = 0f;
        ShowFace(Face.Back);
    }

    private void ShowFace(Face value)
    {
        face = value;
        UIFactory.Clear(card);
        Image back = UIFactory.Panel("Face", card, UIFactory.PanelLight);
        UIFactory.Stretch(back.rectTransform);
        switch (value)
        {
            case Face.Back:
                UIFactory.Frame("Frame", back.transform, UIFactory.LineColor, 3f);
                Big(back.transform, "?", 160, UIFactory.LineColor);
                break;
            case Face.Depleted:
                UIFactory.Frame("Frame", back.transform, UIFactory.LineColor, 3f);
                Big(back.transform, "이미 받았다", 40, ArtifactUIText.Muted);
                break;
            case Face.Lost:
                UIFactory.Frame("Frame", back.transform, UIFactory.LineColor, 3f);
                Big(back.transform, "빈손", 64, ArtifactUIText.Muted);
                break;
            case Face.Won:
                BuildWonCard(back.rectTransform, shownArtifact);
                break;
        }
    }

    private static void Big(Transform parent, string text, int size, Color color)
    {
        Text label = UIFactory.Label("Text", parent, text, size, TextAnchor.MiddleCenter, color);
        UIFactory.Stretch(label.rectTransform);
    }

    // 당첨 4요소 카드: 아이콘, 이름, 세트·등급, 옵션 + 충돌 경고
    private static void BuildWonCard(RectTransform face, ArtifactDefinition def)
    {
        if (def == null) return;
        UIFactory.Frame("GradeFrame", face, ArtifactGradeUtility.Color(def.Grade), 4f);
        Image icon = UIFactory.ArtifactIcon("Icon", face, def);
        UIFactory.Place(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(120f, 120f));
        float width = CardSize.x - 48f;
        float y = -156f;
        y -= ArtifactCardParts.Header(face, def, new Vector2(24f, y), width, -1, 30) + 10f;
        y -= ArtifactCardParts.Rows(face, "Option", ArtifactUIText.PlainRows(def), new Vector2(24f, y), width, 20) + 6f;
        ArtifactCardParts.Warnings(face, ArtifactUIText.WarningRows(def), new Vector2(24f, y), width, 20);
    }

    // 보유 런 재화와 시도 비용. 부족하면 비용을 빨갛게
    private void RefreshCost()
    {
        int balance = GameServices.Wallet != null ? GameServices.Wallet.Balance : 0;
        if (session.State != GambleState.Active)
        {
            costText.text = $"보유 ● {balance}";
            return;
        }
        string cost = $"시도 비용 ● {session.Cost}";
        if (balance < session.Cost) cost = $"<color=#{ArtifactUIText.Hex(ArtifactUIText.Bad)}>{cost}</color>";
        costText.text = $"보유 ● {balance}   ·   {cost}";
    }

    public override void Tick()
    {
        if (session == null) return;
        RefreshCost();
        GambleBalance b = GameBalance.Current.Gamble;
        chance.text = session.State == GambleState.Depleted ? "" : $"당첨 확률 {session.CurrentChance * 100f:0}% · 실패마다 +{b.PityIncrement * 100f:0}%";

        if (Revealing)
        {
            revealTimer += Time.unscaledDeltaTime;
            // 0 → FlipTime: 뒷면 접힘, → 2배: 결과 펼침
            if (revealTimer < FlipTime) card.localScale = new Vector3(1f - revealTimer / FlipTime, 1f, 1f);
            else if (revealTimer < FlipTime * 2f)
            {
                if (face != pendingFace) ShowFace(pendingFace);
                card.localScale = new Vector3((revealTimer - FlipTime) / FlipTime, 1f, 1f);
            }
            else
            {
                if (face != pendingFace) ShowFace(pendingFace);
                card.localScale = Vector3.one;
                bool done = pendingFace != Face.Won || revealTimer >= FlipTime * 2f + WonHold;
                if (done)
                {
                    revealTimer = -1f;
                    // 즉시 장착 못 하면 GrantImmediate가 도박을 닫고 획득 대기 모드를 엶
                    if (pendingFace == Face.Won) session.Deliver();
                }
            }
        }
        attempt.interactable = !Revealing && session.CanAttempt;
    }
}
