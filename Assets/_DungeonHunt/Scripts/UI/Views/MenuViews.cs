using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// 첫 화면 (08 §3): 게임 시작, 게임 종료
public class FirstScreenView : UIView
{
    private Action onStart, onQuit;
    public override ModalKind Kind => ModalKind.Screen;
    public override bool ClosesOnEsc => false;

    protected override void Build(RectTransform root)
    {
        Image bg = UIFactory.Panel("Background", root, new Color(0.05f, 0.05f, 0.08f, 1f));
        UIFactory.Stretch(bg.rectTransform);
        Text title = UIFactory.Label("Title", root, "던전헌트", 110, TextAnchor.MiddleCenter, UIFactory.AccentColor);
        UIFactory.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 220f), new Vector2(1200f, 160f));
        Text sub = UIFactory.Label("Sub", root, "DungeonHunt", 36, TextAnchor.MiddleCenter, new Color(0.7f, 0.7f, 0.75f));
        UIFactory.Place(sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 130f), new Vector2(800f, 60f));
        Button start = UIFactory.Button("Start", root, "게임 시작", () => onStart?.Invoke(), 36);
        UIFactory.Place((RectTransform)start.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(420f, 80f));
        Button quit = UIFactory.Button("Quit", root, "게임 종료", () => onQuit?.Invoke(), 36);
        UIFactory.Place((RectTransform)quit.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -140f), new Vector2(420f, 80f));
    }

    public void Setup(Action start, Action quit)
    {
        onStart = start;
        onQuit = quit;
    }
}

// 로비 (08 §3): 탐사 시작, 영구재화 상점, 아티팩트 도감, 돌아가기
public class LobbyView : UIView
{
    private Action onStart, onShop, onCodex, onBack;
    private Text currencyText;
    private Text statsText;
    public override ModalKind Kind => ModalKind.Screen;
    public override bool ClosesOnEsc => false;

    protected override void Build(RectTransform root)
    {
        Image bg = UIFactory.Panel("Background", root, new Color(0.07f, 0.07f, 0.1f, 1f));
        UIFactory.Stretch(bg.rectTransform);
        Text title = UIFactory.Label("Title", root, "로비", 80, TextAnchor.MiddleCenter, UIFactory.AccentColor);
        UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(800f, 120f));
        currencyText = UIFactory.Label("Currency", root, "", 34, TextAnchor.UpperRight);
        UIFactory.Place(currencyText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-60f, -60f), new Vector2(700f, 50f));
        statsText = UIFactory.Label("Stats", root, "", 24, TextAnchor.LowerLeft, new Color(0.75f, 0.75f, 0.8f));
        UIFactory.Place(statsText.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(60f, 60f), new Vector2(800f, 140f));

        string[] labels = { "탐사 시작", "영구재화 상점", "아티팩트 도감", "돌아가기" };
        Action[] actions = { () => onStart?.Invoke(), () => onShop?.Invoke(), () => onCodex?.Invoke(), () => onBack?.Invoke() };
        for (int i = 0; i < labels.Length; i++)
        {
            Button b = UIFactory.Button(labels[i], root, labels[i], actions[i], 36);
            UIFactory.Place((RectTransform)b.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f - i * 100f), new Vector2(460f, 80f));
        }
    }

    public void Setup(Action start, Action shop, Action codex, Action back)
    {
        onStart = start; onShop = shop; onCodex = codex; onBack = back;
    }

    public override void Tick()
    {
        PermanentData data = GameServices.Save != null ? GameServices.Save.Data : null;
        if (data == null) return;
        currencyText.text = $"영구 재화  {data.PermanentCurrency}";
        statsText.text = $"누적 클리어 {data.RunClearedCount}회 · 최고 도달 {data.HighestFloorReached}층\n치명타 확률 강화 {data.CritChanceLevel}단계 · 치명타 피해 강화 {data.CritDamageLevel}단계";
    }
}

// 영구재화 상점 (상품/가격/내부 화면은 기획 미정 → 06 §5.3 후보 상품과 PrototypeDefault 비용으로 잠정 구성)
public class PermanentShopView : UIView
{
    private RectTransform list;
    private Text currencyText;
    public override ModalKind Kind => ModalKind.Screen;

    protected override void Build(RectTransform root)
    {
        RectTransform panel = CenterPanel(root, new Vector2(1500f, 900f));
        Title(panel, "영구재화 상점 (잠정)");
        currencyText = UIFactory.Label("Currency", panel, "", 30, TextAnchor.UpperRight);
        UIFactory.Place(currencyText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -40f), new Vector2(500f, 40f));
        list = UIFactory.CreateRect("List", panel);
        UIFactory.Stretch(list, 0f);
        list.offsetMin = new Vector2(40f, 120f);
        list.offsetMax = new Vector2(-40f, -110f);
        Button back = UIFactory.Button("Back", panel, "뒤로", () => Manager.CloseView(this));
        UIFactory.Place((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(300f, 70f));
    }

    protected override void OnOpened() => Refresh();

    private void Refresh()
    {
        UIFactory.Clear(list);
        PermanentData data = GameServices.Save.Data;
        currencyText.text = $"보유 영구 재화  {data.PermanentCurrency}";

        // 기본 스탯 강화 (치명타 확률, 치명타 피해)
        StatId[] stats = { StatId.CritChance, StatId.CritDamage };
        for (int i = 0; i < stats.Length; i++)
        {
            StatId stat = stats[i];
            int level = MetaProgression.StatLevel(stat);
            int cost = MetaProgression.NextStatCost(stat);
            string label = $"{StatNames.Display(stat)} 강화  {level}/{MetaProgression.MaxStatLevel}" + (cost >= 0 ? $"  (비용 {cost})" : "  (최대)");
            Button b = UIFactory.Button("Stat" + i, list, label, () =>
            {
                if (MetaProgression.Upgrade(stat))
                    GameServices.Run.SaveWithRecovery(GameServices.Save.Data, null, null, blockingInRun: false);
                Refresh();
            }, 26);
            b.interactable = MetaProgression.CanUpgrade(stat);
            UIFactory.Place((RectTransform)b.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(i * 710f, 0f), new Vector2(690f, 64f));
        }

        // 재화 해금 몫 아티팩트
        Text header = UIFactory.Label("Header", list, $"아티팩트 해금 (각 {MetaProgression.UnlockCost})", 28, TextAnchor.UpperLeft, UIFactory.AccentColor);
        UIFactory.Place(header.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -90f), new Vector2(900f, 40f));
        ArtifactCatalog catalog = GameServices.Config != null ? GameServices.Config.Artifacts : null;
        if (catalog == null) return;
        List<ArtifactDefinition> purchasable = catalog.Artifacts.Where(a => a != null && a.UnlockSource == ArtifactUnlockSource.Currency).ToList();
        if (purchasable.Count == 0)
        {
            Text none = UIFactory.Label("None", list, "재화 해금 대상이 없음 (카탈로그 해금 출처 배정 미정)", 24, TextAnchor.UpperLeft, new Color(0.7f, 0.7f, 0.7f));
            UIFactory.Place(none.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -140f), new Vector2(1200f, 40f));
            return;
        }
        for (int i = 0; i < purchasable.Count; i++)
        {
            ArtifactDefinition def = purchasable[i];
            bool unlocked = MetaProgression.IsUnlocked(def);
            // 미해금 아티팩트는 정체를 숨김 (도감 물음표와 같은 원칙)
            string label = unlocked ? $"{def.DisplayName} (해금됨)" : $"잠긴 아티팩트 #{i + 1}";
            Button b = UIFactory.Button("Unlock" + i, list, label, () =>
            {
                if (MetaProgression.BuyUnlock(def))
                    GameServices.Run.SaveWithRecovery(GameServices.Save.Data, null, null, blockingInRun: false);
                Refresh();
            }, 22);
            b.interactable = MetaProgression.CanBuyUnlock(def);
            int col = i % 4, row = i / 4;
            UIFactory.Place((RectTransform)b.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(col * 355f, -140f - row * 74f), new Vector2(340f, 64f));
        }
    }
}

// 아티팩트 도감 (08 §3.2): 격자는 세트 그룹 + 범용 그룹, 슬롯 상태 물음표/실루엣/아이콘, 같은 화면 상세 패널
public class CodexView : UIView
{
    private RectTransform grid;
    private Text detail;
    public override ModalKind Kind => ModalKind.Screen;

    protected override void Build(RectTransform root)
    {
        RectTransform panel = CenterPanel(root, new Vector2(1700f, 960f));
        Title(panel, "아티팩트 도감");
        grid = UIFactory.CreateRect("Grid", panel);
        UIFactory.Place(grid, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -110f), new Vector2(1060f, 760f));
        Image detailPanel = UIFactory.Panel("Detail", panel, UIFactory.PanelLight);
        UIFactory.Place(detailPanel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -110f), new Vector2(520f, 640f));
        detail = UIFactory.Label("DetailText", detailPanel.transform, "", 26, TextAnchor.UpperLeft);
        UIFactory.Stretch(detail.rectTransform, 24f);
        Button back = UIFactory.Button("Back", panel, "뒤로", () => Manager.CloseView(this));
        UIFactory.Place((RectTransform)back.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 40f), new Vector2(300f, 70f));
    }

    protected override void OnOpened()
    {
        UIFactory.Clear(grid);
        detail.text = "발견한 아티팩트가 없습니다";
        ArtifactCatalog catalog = GameServices.Config != null ? GameServices.Config.Artifacts : null;
        if (catalog == null) return;

        // 그룹 나열 순서는 기획 미정 → 세트 CodexGroupOrder, 범용은 마지막
        var groups = new List<(string title, List<ArtifactDefinition> items)>();
        foreach (ArtifactSetDefinition set in catalog.Sets.Where(s => s != null).OrderBy(s => s.CodexGroupOrder))
            groups.Add((set.DisplayName, catalog.Artifacts.Where(a => a != null && a.Set == set).OrderBy(a => a.CodexOrder).ToList()));
        groups.Add(("범용", catalog.Artifacts.Where(a => a != null && a.Set == null).OrderBy(a => a.CodexOrder).ToList()));

        ArtifactDefinition firstDiscovered = null;
        float y = 0f;
        foreach (var group in groups)
        {
            Text header = UIFactory.Label("Group", grid, group.title, 24, TextAnchor.UpperLeft, UIFactory.AccentColor);
            UIFactory.Place(header.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, y), new Vector2(300f, 34f));
            for (int i = 0; i < group.items.Count; i++)
            {
                ArtifactDefinition def = group.items[i];
                bool unlocked = MetaProgression.IsUnlocked(def);
                bool discovered = unlocked && MetaProgression.IsDiscovered(def);
                string label = !unlocked ? "?" : discovered ? def.DisplayName : "■";
                Button slot = UIFactory.Button("Slot", grid, label, discovered ? (Action)(() => ShowDetail(def)) : null, 18);
                slot.interactable = discovered;
                slot.GetComponent<Image>().color = discovered ? ArtifactGradeUtility.Color(def.Grade) * 0.6f + Color.black * 0.4f : UIFactory.DisabledColor;
                UIFactory.Place((RectTransform)slot.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(320f + i * 180f, y), new Vector2(170f, 70f));
                if (discovered && firstDiscovered == null) firstDiscovered = def;
            }
            y -= 90f;
        }
        if (firstDiscovered != null) ShowDetail(firstDiscovered);
    }

    private void ShowDetail(ArtifactDefinition def)
    {
        string set = def.Set != null ? def.Set.DisplayName + " 세트" : "범용";
        detail.text = $"<size=34><color=#{ColorUtility.ToHtmlStringRGB(ArtifactGradeUtility.Color(def.Grade))}>{def.DisplayName}</color></size>\n{ArtifactGradeUtility.Display(def.Grade)} · {set}\n\n{def.EffectText}";
    }
}

// 결과창 (08 §3): 결과, 도달 층, 처치한 전투룸 수, RunTime, 영구 재화 획득 예정량, 진행 해금 카드. 버튼은 로비로 하나
public class ResultView : UIView
{
    private Text body;
    private RectTransform cards;
    private Action onLobby;
    public override ModalKind Kind => ModalKind.Screen;
    public override bool ClosesOnEsc => false;

    protected override void Build(RectTransform root)
    {
        Image bg = UIFactory.Panel("Background", root, new Color(0.04f, 0.04f, 0.06f, 1f));
        UIFactory.Stretch(bg.rectTransform);
        body = UIFactory.Label("Body", root, "", 40, TextAnchor.UpperCenter);
        UIFactory.Place(body.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(1200f, 480f));
        cards = UIFactory.CreateRect("Cards", root);
        UIFactory.Place(cards, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -120f), new Vector2(1200f, 360f));
        Button lobby = UIFactory.Button("ToLobby", root, "로비로", () => onLobby?.Invoke(), 36);
        UIFactory.Place((RectTransform)lobby.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 80f), new Vector2(400f, 80f));
    }

    public void Setup(ResultInfo info, List<ArtifactDefinition> unlocks, Action toLobby)
    {
        onLobby = toLobby;
        string result = info.Cleared ? "<color=#FFD24D>런 클리어</color>" : "<color=#FF6060>게임 오버</color>";
        body.text = $"<size=72>{result}</size>\n\n도달 층  {info.FloorReached}층\n처치한 전투룸  {info.RoomsCleared}개\n플레이 시간  {UIFactory.FormatTime(info.RunTime)}\n영구 재화 획득 예정  {info.PermanentCurrency}";
        ProgressCardsView.LayoutCards(cards, unlocks);
    }
}

// 진행 해금 카드 패널 (08 §3.3): 최대 3장 가운데 정렬, 확인/ESC로 닫음
public class ProgressCardsView : UIView
{
    private RectTransform cards;
    private Action onClosed;
    public override ModalKind Kind => ModalKind.System;

    protected override void Build(RectTransform root)
    {
        RectTransform panel = CenterPanel(root, new Vector2(1300f, 720f));
        Title(panel, "새 아티팩트 해금");
        cards = UIFactory.CreateRect("Cards", panel);
        UIFactory.Place(cards, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(1200f, 420f));
        Button ok = UIFactory.Button("OK", panel, "확인", () => Manager.CloseView(this));
        UIFactory.Place((RectTransform)ok.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(300f, 70f));
    }

    public void Setup(List<ArtifactDefinition> unlocks, Action closed)
    {
        onClosed = closed;
        LayoutCards(cards, unlocks);
    }

    protected override void OnClosed()
    {
        Action callback = onClosed;
        onClosed = null;
        callback?.Invoke();
    }

    public static void LayoutCards(RectTransform parent, List<ArtifactDefinition> unlocks)
    {
        UIFactory.Clear(parent);
        int count = Mathf.Min(3, unlocks.Count);
        float width = 340f, gap = 30f;
        float start = -(count - 1) * (width + gap) * 0.5f;
        for (int i = 0; i < count; i++)
        {
            RectTransform card = UIFactory.ArtifactCard("Card", parent, unlocks[i], new Vector2(width, 400f), true, "다음 런부터 등장");
            UIFactory.Place(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(start + i * (width + gap), 0f), new Vector2(width, 400f));
        }
    }
}

// 최종 엔딩 (10 §5.1). 화면 형식/문구는 미정 → 마지막 대사만 표시하는 자리 표시용
public class EndingView : UIView
{
    private Text line;
    public override ModalKind Kind => ModalKind.System;
    public override bool ClosesOnEsc => false;

    protected override void Build(RectTransform root)
    {
        Image bg = UIFactory.Panel("Background", root, Color.black);
        UIFactory.Stretch(bg.rectTransform);
        line = UIFactory.Label("Line", root, "", 44, TextAnchor.MiddleCenter);
        UIFactory.Stretch(line.rectTransform, 200f);
    }

    public void Setup(string lastLine) => line.text = $"<size=30><color=#888888>엔딩 (화면 형식 미정)</color></size>\n\n\"{lastLine}\"";

    public override void OnEscape() { }
}
