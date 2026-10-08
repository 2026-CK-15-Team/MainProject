using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// 08 UI 총괄: 화면 상태, 모달 정책(단일 모달, 시간 정지/비정지/시스템), ESC 처리 순서, 같은 틱 입력 우선순위,
// 페이드, 커서 제한, 포커스 이탈 정지. UI는 코드로 조립 (아트 들어오면 각 View의 Build만 교체)
[DefaultExecutionOrder(-350)]
public class UIManager : MonoBehaviour
{
    private Canvas canvas;
    private RectTransform root;
    private RectTransform systemRoot;
    private Image fade;
    private readonly List<UIView> views = new List<UIView>();

    private FirstScreenView firstScreen;
    private LobbyView lobby;
    private PermanentShopView permanentShop;
    private CodexView codex;
    private ResultView result;
    private ProgressCardsView progressCards;
    private EndingView ending;
    private HudView hud;
    private OnboardingView onboarding;
    private ExpandedMapView expandedMap;
    private AcquireView acquire;
    private InventoryView inventory;
    private ShopView shop;
    private GambleView gamble;
    private PauseMenuView pauseMenu;
    private ControlsView controls;
    private SettingsView settings;
    private SystemPopupView popup;
    private SaveFailureView saveFailure;
    private FocusOverlayView focusOverlay;

    public bool IsModalOpen => ActiveModal(ModalKind.NonStop) != null || ActiveModal(ModalKind.TimeStop) != null || ActiveModal(ModalKind.System) != null;

    private void Awake()
    {
        GameServices.UI = this;
        BuildCanvas();
    }

    private void BuildCanvas()
    {
        // 화면 / 페이드 / 시스템 팝업 3단 캔버스. 저장 실패 팝업은 검은 화면 위에도 보여야 함 (08 §8.4)
        root = CreateCanvas("UICanvas", 100);
        RectTransform fadeRoot = CreateCanvas("FadeCanvas", 200);
        systemRoot = CreateCanvas("SystemCanvas", 300);

        if (FindFirstObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.transform.SetParent(transform, false);
            var system = es.AddComponent<EventSystem>();
            system.sendNavigationEvents = false; // 메뉴는 마우스만 (08 §3)
            es.AddComponent<InputSystemUIInputModule>();
        }

        // 생성 순서 = 그리기 순서
        hud = Add(new HudView(), "HUD", root);
        onboarding = Add(new OnboardingView(), "Onboarding", root);
        expandedMap = Add(new ExpandedMapView(), "ExpandedMap", root);
        acquire = Add(new AcquireView(), "Acquire", root);
        inventory = Add(new InventoryView(), "Inventory", root);
        shop = Add(new ShopView(), "Shop", root);
        gamble = Add(new GambleView(), "Gamble", root);
        firstScreen = Add(new FirstScreenView(), "FirstScreen", root);
        lobby = Add(new LobbyView(), "Lobby", root);
        permanentShop = Add(new PermanentShopView(), "PermanentShop", root);
        codex = Add(new CodexView(), "Codex", root);
        result = Add(new ResultView(), "Result", root);
        pauseMenu = Add(new PauseMenuView(), "PauseMenu", root);
        controls = Add(new ControlsView(), "Controls", root);
        settings = Add(new SettingsView(), "Settings", root);

        fade = UIFactory.Panel("Fade", fadeRoot, new Color(0, 0, 0, 0));
        UIFactory.Stretch(fade.rectTransform);
        fade.raycastTarget = false;

        ending = Add(new EndingView(), "Ending", systemRoot);
        progressCards = Add(new ProgressCardsView(), "ProgressCards", systemRoot);
        popup = Add(new SystemPopupView(), "Popup", systemRoot);
        saveFailure = Add(new SaveFailureView(), "SaveFailure", systemRoot);
        focusOverlay = Add(new FocusOverlayView(), "FocusOverlay", systemRoot);
    }

    private RectTransform CreateCanvas(string name, int order)
    {
        var canvasGo = new GameObject(name, typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        var c = canvasGo.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = order;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        Vector2Int reference = GameBalance.Current.UI.ReferenceResolution;
        scaler.referenceResolution = new Vector2(reference.x, reference.y);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        if (canvas == null) canvas = c;
        return (RectTransform)canvasGo.transform;
    }

    private T Add<T>(T view, string name, RectTransform parent) where T : UIView
    {
        view.Create(this, parent, name);
        views.Add(view);
        return view;
    }

    // ---------------- 매 프레임 ----------------

    private void Update()
    {
        foreach (UIView view in views)
            if (view.IsOpen) view.Tick();

        RunManager run = GameServices.Run;
        bool inRun = run != null && run.State == RunState.InRun;
        InputGate.GameplayBlocked = !inRun || IsModalOpen || run.IsTransitioning || run.IsEndingSequence;
        UpdateCursor(inRun);

        if (run == null) return;
        HandleKeys(run);
    }

    // 같은 틱 입력 순서: 열린 모달의 입력 > ESC > 유효한 F > Tab > I > 게임 행동 (01-1 §7.2)
    private void HandleKeys(RunManager run)
    {
        if (InputGate.KeyDown(Key.Escape))
        {
            HandleEscape(run);
            InputGate.ConsumeFrame();
            return;
        }
        if (run.State != RunState.InRun || run.IsTransitioning || run.IsEndingSequence) return;

        // 열린 모달 자기 키로 닫기 (Tab → 확대 미니맵, I → 보유 아티팩트)
        if (expandedMap.IsOpen && InputGate.KeyDown(Key.Tab)) { CloseView(expandedMap); InputGate.ConsumeFrame(); return; }
        if (inventory.IsOpen && !inventory.IsPendingMode && InputGate.KeyDown(Key.I)) { CloseView(inventory); InputGate.ConsumeFrame(); return; }
        if (IsModalOpen) return;

        PlayerController player = GameServices.Player;
        if (player == null) return;

        if (InputGate.KeyDown(Key.F) && player.Interactor.TryInteract(player))
        {
            InputGate.ConsumeFrame();
            return;
        }
        // 실행할 수 없는 UI 입력은 뒤 입력을 막지 않음
        if (InputGate.KeyDown(Key.Tab) && CanOpenGameplayModal(player))
        {
            OpenModal(expandedMap);
            return;
        }
        if (InputGate.KeyDown(Key.I) && CanOpenGameplayModal(player))
        {
            inventory.SetupBrowse();
            OpenModal(inventory);
        }
    }

    // 잠긴 전투룸/보스룸, 도박 상호작용 중, 회피 중에는 열리지 않음 (08 §5.1, §5.2, §8.1)
    private bool CanOpenGameplayModal(PlayerController player)
    {
        if (player.Movement.IsDodging) return false;
        FloorDirector floor = GameServices.Floor;
        if (floor != null && floor.CurrentRoom != null && !floor.InCorridor && floor.CurrentRoom.BlocksModal) return false;
        return true;
    }

    // 08 §6.1 ESC 처리 순서
    private void HandleEscape(RunManager run)
    {
        // 1. 버튼으로만 닫는 시스템 팝업이 있으면 무시
        if (saveFailure.IsOpen || focusOverlay.IsOpen || (popup.IsOpen && !popup.ClosesOnEsc)) return;
        // 2. 엔딩 구간은 RunManager가 판정 (EndingSkipPressed)
        if (run.IsEndingSequence) return;
        // 3. 일시정지를 제외한 닫기 UI가 있으면 최상단을 닫음
        UIView top = TopClosable();
        if (top != null)
        {
            top.OnEscape();
            return;
        }
        // 4. 온보딩, 결과 화면, 층 전환 중이면 무시
        if (onboarding.IsOpen || run.IsResultScreen || run.IsTransitioning) return;
        if (run.State != RunState.InRun) return;
        // 5. 일시정지 열려 있으면 재개, 아니면 열기 (층 도착 안내는 유지)
        if (pauseMenu.IsOpen) CloseView(pauseMenu);
        else OpenModal(pauseMenu);
    }

    private UIView TopClosable()
    {
        for (int i = views.Count - 1; i >= 0; i--)
        {
            UIView view = views[i];
            if (!view.IsOpen || view == pauseMenu || view == hud || view == onboarding) continue;
            if (view.Kind == ModalKind.Screen && (view == firstScreen || view == lobby || view == result)) continue;
            if (!view.ClosesOnEsc) continue;
            return view;
        }
        return null;
    }

    private UIView ActiveModal(ModalKind kind)
    {
        foreach (UIView view in views)
            if (view.IsOpen && view.Kind == kind) return view;
        return null;
    }

    // 단일 모달 제한: 비정지/설정 모달은 하나만 (시스템 오버레이는 예외)
    private bool OpenModal(UIView view)
    {
        if (view.Kind == ModalKind.NonStop && (ActiveModal(ModalKind.NonStop) != null || ActiveModal(ModalKind.TimeStop) != null)) return false;
        view.Open();
        InputGate.ConsumeFrame();
        GameServices.Player?.Input.LatchAttack();
        return true;
    }

    public void CloseView(UIView view)
    {
        view.Close();
        // 모달을 닫은 클릭은 게임으로 전달 안 됨: 뗐다가 다시 눌러야 공격 (08 §8.1)
        GameServices.Player?.Input.LatchAttack();
        InputGate.ConsumeFrame();
    }

    public void CloseAllModals()
    {
        foreach (UIView view in views)
            if (view.IsOpen && (view.Kind == ModalKind.NonStop || view.Kind == ModalKind.TimeStop)) view.Close();
    }

    // ---------------- 화면 ----------------

    public void HideMenus()
    {
        firstScreen.Close();
        lobby.Close();
        permanentShop.Close();
        codex.Close();
        result.Close();
        progressCards.Close();
    }

    public void ShowFirstScreen(Action onStart, Action onQuit)
    {
        HideMenus();
        ShowHUD(false);
        firstScreen.Setup(onStart, onQuit);
        firstScreen.Open();
    }

    public void ShowLobby(Action onStart, Action onShop, Action onCodex, Action onBack)
    {
        HideMenus();
        ShowHUD(false);
        lobby.Setup(onStart, onShop, onCodex, onBack);
        lobby.Open();
    }

    public void ShowPermanentShop() => permanentShop.Open();
    public void ShowCodex() => codex.Open();

    public void ShowHUD(bool show)
    {
        if (hud == null) return;
        if (show) { hud.Open(); hud.Root.transform.SetAsFirstSibling(); }
        else hud.Close();
    }

    public void ShowResult(ResultInfo info, List<ArtifactDefinition> cards, Action onToLobby)
    {
        CloseAllModals();
        ShowHUD(false);
        result.Setup(info, cards, onToLobby);
        result.Open();
    }

    public void ShowProgressCards(List<ArtifactDefinition> cards, Action onClosed)
    {
        progressCards.Setup(cards, onClosed);
        progressCards.Open();
    }

    public void ShowOnboarding(bool show)
    {
        if (show) onboarding.Open();
        else onboarding.Close();
    }

    public void ShowFloorArrival(int floor, string name) => hud.ShowFloorArrival(floor, name);
    public void ShowCenterToast(string text, float duration) => hud.ShowCenterToast(text, duration);
    public void QueueSetToast(string text, float duration) => hud.QueueSetToast(text, duration);
    public void ShowBossBar(FinalBoss boss) => hud.ShowBossBar(boss);
    public void ShowWaitIndicator(bool show) => hud.SetWaitIndicator(show);

    public void ShowEnding(string lastLine)
    {
        ending.Setup(lastLine);
        ending.Open();
    }

    public void HideEnding() => ending.Close();

    public bool EndingSkipPressed() => InputGate.KeyDown(Key.Escape);

    public void OpenControls() => controls.Open();
    public void OpenSettings() => settings.Open();

    // ---------------- 게임플레이 팝업 ----------------

    public void OpenAcquire(ArtifactPickup pickup)
    {
        acquire.Setup(pickup);
        OpenModal(acquire);
    }

    // 획득 대기 모드는 강제 개방 (07 §4.10 3번). 다른 비정지 모달이 열려 있으면 닫고 엶
    public void OpenInventoryPending(ArtifactDefinition def, AcquisitionOrigin origin, Action onEquipped, Action onCancelled)
    {
        foreach (UIView view in views)
            if (view.IsOpen && view.Kind == ModalKind.NonStop) view.Close();
        inventory.SetupPending(def, origin, onEquipped, onCancelled);
        OpenModal(inventory);
    }

    public void OpenShop(ShopRoomController room)
    {
        shop.Setup(room);
        OpenModal(shop);
    }

    public void OpenGamble(GambleSession session, GambleRoomController owner)
    {
        gamble.Setup(session, owner);
        OpenModal(gamble);
    }

    // ---------------- 시스템 팝업 ----------------

    public void ShowSystemNotice(string message, string button, Action onClosed)
    {
        popup.Setup("안내", message, button, onClosed);
        popup.Open();
    }

    public void ShowConfirm(string title, string message, Action onYes, Action onNo, string yes = "확인", string no = "취소")
    {
        popup.Setup(title, message, yes, onYes, no, onNo, escCancelsPopup: true);
        popup.Open();
    }

    public void ShowSaveFailure(bool second, Func<bool> onRetry, Action onFirstScreen)
    {
        saveFailure.Setup(second, onRetry, onFirstScreen);
        saveFailure.Open();
    }

    // 활성 런에서 포커스를 잃으면 오버레이 + 자동 일시정지, 다시 얻어도 자동 재개 안 함 (08 §8.3)
    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus) return;
        RunManager run = GameServices.Run;
        if (run == null || run.State != RunState.InRun || focusOverlay.IsOpen) return;
        focusOverlay.Open();
    }

    // ---------------- 페이드 ----------------

    public void SetBlack(bool black) => SetFade(black ? 1f : 0f);

    public void SetFade(float alpha)
    {
        fade.color = new Color(0f, 0f, 0f, Mathf.Clamp01(alpha));
        fade.raycastTarget = alpha > 0.99f;
    }

    public IEnumerator FadeOut(float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            SetFade(t / Mathf.Max(0.0001f, duration));
            yield return null;
        }
        SetFade(1f);
    }

    public IEnumerator FadeIn(float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            SetFade(1f - t / Mathf.Max(0.0001f, duration));
            yield return null;
        }
        SetFade(0f);
    }

    // 활성 런 + 모달/일시정지 없음 → 커서를 창 안에 제한 (08 §8.3)
    private void UpdateCursor(bool inRun)
    {
        bool confine = inRun && !IsModalOpen && !GameClock.IsPaused && Application.isFocused;
        Cursor.lockState = confine ? CursorLockMode.Confined : CursorLockMode.None;
    }
}
