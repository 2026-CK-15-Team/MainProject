using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// 06-1 §8 영구재화 상점 (Lobby 하위 전체 화면). 탭: 아티팩트 해금 / 성장 포인트(진입 시 선택)
// 노드 입력: 좌클릭 배치, 우클릭 해제(가장 위 배치 노드만). 구매, 배치, 해제, 초기화는 확인창 없이 즉시 저장
public class PermanentShopView : UIView
{
    private static readonly Color ActiveNode = new Color(0.2f, 0.24f, 0.32f, 1f);
    private static readonly Color AllocatedNode = new Color(0.22f, 0.42f, 0.8f, 1f);
    private static readonly Color LockedNode = new Color(0.14f, 0.14f, 0.16f, 1f);
    private static readonly Color AvailableBorder = new Color(0.45f, 0.7f, 1f, 1f);
    private static readonly Color ShortFlash = new Color(0.75f, 0.2f, 0.22f, 1f);
    private const float FlashDuration = 0.25f;

    private class NodeWidget
    {
        public int Category, Node;
        public Image Background;
        public GameObject SolidFrame, DashFrame;
        public Text Cost, Effect;
        public float FlashUntil = -1f;
    }

    private Text currencyText;
    private Button growthTab, unlockTab;
    private RectTransform growthRoot, unlockRoot, unlockGrid, treeArea;
    private bool growthSelected = true;

    // 성장 포인트 탭
    private Text remainingText, purchasedText, effectsText;
    private RectTransform pointDots;
    private Button buyButton, resetButton;
    private readonly List<NodeWidget> nodes = new List<NodeWidget>();
    private NodeWidget hovered;

    public override ModalKind Kind => ModalKind.Screen;

    protected override void Build(RectTransform root)
    {
        Image bg = UIFactory.Panel("Background", root, new Color(0.07f, 0.07f, 0.1f, 1f));
        UIFactory.Stretch(bg.rectTransform);
        Text title = UIFactory.Label("Title", root, "영구재화 상점", 52, TextAnchor.MiddleLeft, UIFactory.AccentColor);
        UIFactory.Place(title.rectTransform, TL, TL, new Vector2(60f, -40f), new Vector2(700f, 70f));
        currencyText = UIFactory.Label("Currency", root, "", 34, TextAnchor.MiddleRight);
        UIFactory.Place(currencyText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-60f, -40f), new Vector2(600f, 70f));

        unlockTab = UIFactory.Button("UnlockTab", root, "아티팩트 해금", () => SelectTab(false), 28);
        UIFactory.Place((RectTransform)unlockTab.transform, TL, TL, new Vector2(60f, -125f), new Vector2(280f, 60f));
        growthTab = UIFactory.Button("GrowthTab", root, "성장 포인트", () => SelectTab(true), 28);
        UIFactory.Place((RectTransform)growthTab.transform, TL, TL, new Vector2(350f, -125f), new Vector2(280f, 60f));

        growthRoot = UIFactory.CreateRect("Growth", root);
        UIFactory.Stretch(growthRoot);
        unlockRoot = UIFactory.CreateRect("Unlock", root);
        UIFactory.Stretch(unlockRoot);
        BuildGrowthFrame(growthRoot);
        unlockGrid = UIFactory.CreateRect("Cards", unlockRoot);
        UIFactory.Place(unlockGrid, TL, TL, new Vector2(60f, -220f), new Vector2(1800f, 720f));

        Button back = UIFactory.Button("Back", root, "뒤로", () => Manager.CloseView(this), UIFactory.SecondaryColor);
        UIFactory.Place((RectTransform)back.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-60f, 40f), new Vector2(280f, 70f));
    }

    private static Vector2 TL => new Vector2(0f, 1f);

    private void BuildGrowthFrame(RectTransform parent)
    {
        // 포인트 바: 남은 포인트, 구매 수, 점(채움 = 배치에 씀, 테두리 = 구매했지만 남음), 포인트 구매 버튼
        Image bar = UIFactory.Panel("PointBar", parent, UIFactory.PanelLight);
        UIFactory.Place(bar.rectTransform, TL, TL, new Vector2(60f, -210f), new Vector2(1800f, 90f));
        remainingText = UIFactory.Label("Remaining", bar.transform, "", 34, TextAnchor.MiddleLeft);
        UIFactory.Place(remainingText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(30f, 0f), new Vector2(280f, 60f));
        purchasedText = UIFactory.Label("Purchased", bar.transform, "", 26, TextAnchor.MiddleLeft, ArtifactUIText.Muted);
        UIFactory.Place(purchasedText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(310f, 0f), new Vector2(200f, 60f));
        pointDots = UIFactory.CreateRect("Dots", bar.transform);
        UIFactory.Place(pointDots, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(520f, 0f), new Vector2(600f, 30f));
        Text hint = UIFactory.Label("Hint", bar.transform, "좌클릭 배치 · 우클릭 해제", 22, TextAnchor.MiddleRight, ArtifactUIText.Muted);
        UIFactory.Place(hint.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-370f, 0f), new Vector2(360f, 60f));
        buyButton = UIFactory.Button("Buy", bar.transform, "", OnBuy, UIFactory.PrimaryColor, 26);
        UIFactory.Place((RectTransform)buyButton.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-20f, 0f), new Vector2(320f, 64f));

        treeArea = UIFactory.CreateRect("Tree", parent);
        UIFactory.Place(treeArea, TL, TL, new Vector2(60f, -320f), new Vector2(1200f, 600f));

        // 적용 효과 패널: 현재 배치의 합계. 노드에 올리면 배치했을 때의 변화를 함께 표시
        Image effects = UIFactory.Panel("Effects", parent, UIFactory.PanelLight);
        UIFactory.Place(effects.rectTransform, TL, TL, new Vector2(1320f, -320f), new Vector2(540f, 600f));
        Text effectsTitle = UIFactory.Label("Title", effects.transform, "적용 중인 영구 효과", 30, TextAnchor.UpperLeft, UIFactory.AccentColor);
        UIFactory.Place(effectsTitle.rectTransform, TL, TL, new Vector2(28f, -24f), new Vector2(480f, 44f));
        effectsText = UIFactory.Label("List", effects.transform, "", 26, TextAnchor.UpperLeft);
        UIFactory.Place(effectsText.rectTransform, TL, TL, new Vector2(28f, -86f), new Vector2(490f, 500f));

        resetButton = UIFactory.Button("Reset", parent, "전체 초기화", OnReset, UIFactory.SecondaryColor);
        UIFactory.Place((RectTransform)resetButton.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(60f, 40f), new Vector2(280f, 70f));
    }

    // 트리 구조는 GameBalance에서 오므로 처음 열 때 만듦
    private void BuildTree()
    {
        GrowthCategory[] tree = MetaProgression.GrowthTree;
        const float columnWidth = 360f, columnGap = 60f;
        const float nodeHeight = 100f, capstoneHeight = 130f, nodeGap = 34f;
        for (int c = 0; c < tree.Length; c++)
        {
            float x = c * (columnWidth + columnGap);
            Text header = UIFactory.Label("Header", treeArea, tree[c].Name, 32, TextAnchor.MiddleCenter, UIFactory.AccentColor);
            UIFactory.Place(header.rectTransform, TL, TL, new Vector2(x, 0f), new Vector2(columnWidth, 50f));

            // 노드 4(위)부터 노드 1(아래) 순서로 내려가며 배치, 노드끼리 세로 선으로 이음
            int count = tree[c].Nodes.Length;
            float y = -60f;
            float lineTop = 0f, lineBottom = 0f;
            var placed = new List<(int node, float y, float height)>();
            for (int n = count - 1; n >= 0; n--)
            {
                float h = n == count - 1 ? capstoneHeight : nodeHeight;
                placed.Add((n, y, h));
                if (n == count - 1) lineTop = y - h * 0.5f;
                lineBottom = y - h * 0.5f;
                y -= h + nodeGap;
            }
            Image line = UIFactory.Panel("Line", treeArea, UIFactory.LineColor);
            line.raycastTarget = false;
            UIFactory.Place(line.rectTransform, TL, new Vector2(0.5f, 1f), new Vector2(x + columnWidth * 0.5f, lineTop), new Vector2(4f, lineTop - lineBottom));

            foreach (var (node, top, height) in placed)
            {
                float width = node == count - 1 ? columnWidth : columnWidth - 40f;
                nodes.Add(CreateNode(c, node, tree[c].Nodes[node], new Vector2(x + (columnWidth - width) * 0.5f, top), new Vector2(width, height)));
            }
        }
    }

    private NodeWidget CreateNode(int category, int node, GrowthNode data, Vector2 position, Vector2 size)
    {
        var w = new NodeWidget { Category = category, Node = node };
        w.Background = UIFactory.Panel("Node" + (node + 1), treeArea, ActiveNode);
        RectTransform rect = w.Background.rectTransform;
        UIFactory.Place(rect, TL, TL, position, size);
        w.SolidFrame = UIFactory.Frame("Frame", rect, AvailableBorder, 2f).gameObject;
        w.DashFrame = UIFactory.CreateRect("Dash", rect).gameObject;
        Vector2 h = size * 0.5f;
        Vector2[] corners = { new Vector2(-h.x, -h.y), new Vector2(h.x, -h.y), new Vector2(h.x, h.y), new Vector2(-h.x, h.y) };
        for (int i = 0; i < 4; i++) UIFactory.Line("Dash", w.DashFrame.transform, corners[i], corners[(i + 1) % 4], AvailableBorder, 2f, 10f, 6f);

        w.Cost = UIFactory.Label("Cost", rect, $"{MetaProgression.NodeCost(node)}P", 24, TextAnchor.UpperLeft, UIFactory.AccentColor);
        UIFactory.Place(w.Cost.rectTransform, TL, TL, new Vector2(14f, -10f), new Vector2(60f, 30f));
        string effect = string.Join("\n", data.Modifiers.Select(MetaProgression.Describe));
        w.Effect = UIFactory.Label("Effect", rect, effect, 22, TextAnchor.MiddleCenter);
        UIFactory.Stretch(w.Effect.rectTransform, 10f);
        w.Effect.rectTransform.offsetMin = new Vector2(60f, 8f);

        UIPointerHandler input = w.Background.gameObject.AddComponent<UIPointerHandler>();
        input.LeftClick = () => OnNodeLeft(w);
        input.RightClick = () => OnNodeRight(w);
        input.Enter = () => { hovered = w; RefreshEffects(); };
        input.Exit = () => { if (hovered == w) { hovered = null; RefreshEffects(); } };
        return w;
    }

    protected override void OnOpened()
    {
        if (nodes.Count == 0) BuildTree();
        hovered = null;
        SelectTab(true);
    }

    private void SelectTab(bool growth)
    {
        growthSelected = growth;
        growthRoot.gameObject.SetActive(growth);
        unlockRoot.gameObject.SetActive(!growth);
        ((Image)growthTab.targetGraphic).color = growth ? UIFactory.PrimaryColor : UIFactory.SecondaryColor;
        ((Image)unlockTab.targetGraphic).color = growth ? UIFactory.SecondaryColor : UIFactory.PrimaryColor;
        Refresh();
    }

    private void Refresh()
    {
        currencyText.text = $"영구 재화  {GameServices.Save.Data.PermanentCurrency}";
        if (growthSelected) RefreshGrowth();
        else RefreshUnlocks();
    }

    private static void Save() => GameServices.Run.SaveWithRecovery(GameServices.Save.Data, null, null, blockingInRun: false);

    // ---------- 성장 포인트 ----------

    private void OnBuy()
    {
        if (MetaProgression.BuyGrowthPoint()) Save();
        Refresh();
    }

    private void OnReset()
    {
        if (MetaProgression.ResetGrowth()) Save();
        Refresh();
    }

    private void OnNodeLeft(NodeWidget w)
    {
        if (MetaProgression.GetNodeState(w.Category, w.Node) != MetaProgression.NodeState.Available) return;
        if (MetaProgression.Allocate(w.Category, w.Node))
        {
            GameServices.Audio?.PlayUIClick();
            Save();
        }
        else w.FlashUntil = Time.unscaledTime + FlashDuration; // 포인트 부족
        Refresh();
    }

    private void OnNodeRight(NodeWidget w)
    {
        if (!MetaProgression.Release(w.Category, w.Node)) return;
        GameServices.Audio?.PlayUIClick();
        Save();
        Refresh();
    }

    private void RefreshGrowth()
    {
        int purchased = MetaProgression.GrowthPointsPurchased;
        int used = MetaProgression.GrowthPointsUsed;
        int max = MetaProgression.GrowthPointsMax;
        remainingText.text = $"남은 포인트  <size=44>{MetaProgression.GrowthPointsRemaining}</size>";
        purchasedText.text = $"구매 {purchased} / {max}";

        UIFactory.Clear(pointDots);
        for (int i = 0; i < max; i++)
        {
            bool bought = i < purchased;
            Image dot = UIFactory.Panel("Dot" + i, pointDots, bought ? AvailableBorder : UIFactory.LineColor * 0.6f);
            dot.sprite = i < used ? PlaceholderArt.Circle : PlaceholderArt.Ring;
            dot.raycastTarget = false;
            UIFactory.Place(dot.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(i * 34f, 0f), new Vector2(26f, 26f));
        }

        int price = MetaProgression.NextGrowthPointPrice;
        UIFactory.SetButtonText(buyButton, price < 0 ? "최대" : $"포인트 구매 ({price})");
        buyButton.interactable = MetaProgression.CanBuyGrowthPoint;
        resetButton.interactable = used > 0;

        foreach (NodeWidget w in nodes) PaintNode(w);
        RefreshEffects();
    }

    private void PaintNode(NodeWidget w)
    {
        MetaProgression.NodeState state = MetaProgression.GetNodeState(w.Category, w.Node);
        bool affordable = MetaProgression.GrowthPointsRemaining >= MetaProgression.NodeCost(w.Node);
        Color color = state == MetaProgression.NodeState.Allocated ? AllocatedNode
            : state == MetaProgression.NodeState.Available ? ActiveNode
            : LockedNode;
        if (Time.unscaledTime < w.FlashUntil) color = Color.Lerp(color, ShortFlash, (w.FlashUntil - Time.unscaledTime) / FlashDuration);
        w.Background.color = color;
        // 선택가능: 실선 테두리, 포인트가 부족하면 점선
        w.SolidFrame.SetActive(state == MetaProgression.NodeState.Available && affordable);
        w.DashFrame.SetActive(state == MetaProgression.NodeState.Available && !affordable);
        Color text = state == MetaProgression.NodeState.Locked ? ArtifactUIText.Muted : UIFactory.TextColor;
        w.Effect.color = text;
        w.Cost.color = state == MetaProgression.NodeState.Locked ? ArtifactUIText.Muted : UIFactory.AccentColor;
    }

    // 같은 스탯, 같은 종류끼리 합산. 노드에 올려 둔 동안은 배치 후 값을 → 로 병기
    private void RefreshEffects()
    {
        List<(StatId stat, ModifierKind kind, float value)> now = Sum(MetaProgression.GrowthModifiers());
        List<(StatId stat, ModifierKind kind, float value)> after = null;
        if (hovered != null && MetaProgression.GetNodeState(hovered.Category, hovered.Node) == MetaProgression.NodeState.Available)
            after = Sum(MetaProgression.GrowthModifiers(hovered.Category));

        var lines = new List<string>();
        IEnumerable<(StatId stat, ModifierKind kind)> keys = (after ?? now).Select(e => (e.stat, e.kind));
        foreach (var key in keys)
        {
            float a = now.Where(e => e.stat == key.stat && e.kind == key.kind).Sum(e => e.value);
            string line = MetaProgression.Describe(new StatModifier(key.stat, key.kind, a));
            if (after != null)
            {
                float b = after.First(e => e.stat == key.stat && e.kind == key.kind).value;
                if (!Mathf.Approximately(a, b))
                {
                    string next = MetaProgression.Describe(new StatModifier(key.stat, key.kind, b));
                    line = $"{line}  <color=#{ArtifactUIText.Hex(ArtifactUIText.Good)}>→ {next.Substring(StatNames.Display(key.stat).Length + 1)}</color>";
                }
            }
            lines.Add(line);
        }
        effectsText.text = lines.Count > 0 ? string.Join("\n", lines) : $"<color=#{ArtifactUIText.Hex(ArtifactUIText.Muted)}>없음</color>";
    }

    private static List<(StatId stat, ModifierKind kind, float value)> Sum(List<StatModifier> mods)
        => mods.GroupBy(m => (m.Stat, m.Kind)).Select(g => (g.Key.Stat, g.Key.Kind, g.Sum(m => m.Value))).ToList();

    public override void Tick()
    {
        if (!growthSelected) return;
        foreach (NodeWidget w in nodes)
            if (w.FlashUntil > 0f)
            {
                PaintNode(w);
                if (Time.unscaledTime >= w.FlashUntil) w.FlashUntil = -1f;
            }
    }

    // ---------- 아티팩트 해금 ----------

    // 카드 상태: 구매 가능(파란 테두리), 재화 부족(회색), 해금됨(체크). 구매 전에는 물음표와 가격만
    private void RefreshUnlocks()
    {
        UIFactory.Clear(unlockGrid);
        ArtifactCatalog catalog = GameServices.Config != null ? GameServices.Config.Artifacts : null;
        List<ArtifactDefinition> purchasable = catalog == null ? new List<ArtifactDefinition>()
            : catalog.Artifacts.Where(a => a != null && a.UnlockSource == ArtifactUnlockSource.Currency).ToList();
        if (purchasable.Count == 0)
        {
            Text none = UIFactory.Label("None", unlockGrid, "재화 해금 대상이 없음", 26, TextAnchor.UpperLeft, ArtifactUIText.Muted);
            UIFactory.Place(none.rectTransform, TL, TL, Vector2.zero, new Vector2(1200f, 40f));
            return;
        }

        const int columns = 8;
        Vector2 size = new Vector2(205f, 270f);
        const float gap = 22f;
        int cost = MetaProgression.UnlockCost;
        for (int i = 0; i < purchasable.Count; i++)
        {
            ArtifactDefinition def = purchasable[i];
            bool unlocked = MetaProgression.IsUnlocked(def);
            bool canBuy = MetaProgression.CanBuyUnlock(def);
            Image card = UIFactory.Panel("Card" + i, unlockGrid, unlocked ? UIFactory.PanelLight : canBuy ? ActiveNode : LockedNode);
            UIFactory.Place(card.rectTransform, TL, TL, new Vector2((i % columns) * (size.x + gap), -(i / columns) * (size.y + gap)), size);
            Image grade = UIFactory.Panel("Grade", card.transform, ArtifactGradeUtility.Color(def.Grade));
            UIFactory.Place(grade.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(size.x, 8f));

            if (unlocked)
            {
                Text check = UIFactory.Label("Check", card.transform, "V", 72, TextAnchor.MiddleCenter, ArtifactUIText.Good);
                UIFactory.Place(check.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(size.x, 100f));
                Text name = UIFactory.Label("Name", card.transform, def.DisplayName, 22, TextAnchor.MiddleCenter);
                UIFactory.Place(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(size.x - 16f, 50f));
                Text done = UIFactory.Label("Done", card.transform, "해금됨", 24, TextAnchor.MiddleCenter, ArtifactUIText.Muted);
                UIFactory.Place(done.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(size.x, 44f));
                continue;
            }

            if (canBuy) UIFactory.Frame("Frame", card.transform, AvailableBorder, 2f);
            Text q = UIFactory.Label("Unknown", card.transform, "?", 96, TextAnchor.MiddleCenter, canBuy ? UIFactory.TextColor : ArtifactUIText.Muted);
            UIFactory.Place(q.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(size.x, 130f));
            Button buy = UIFactory.Button("Buy", card.transform, $"● {cost}", () =>
            {
                if (MetaProgression.BuyUnlock(def)) Save();
                Refresh();
            }, UIFactory.PrimaryColor, 26);
            buy.interactable = canBuy;
            UIFactory.Place((RectTransform)buy.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(size.x - 30f, 56f));
        }
    }
}
