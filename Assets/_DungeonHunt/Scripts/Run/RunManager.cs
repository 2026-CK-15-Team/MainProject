using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum RunState
{
    Boot,
    FirstScreen,
    Lobby,
    InRun,
    RunCleared,
    RunOver,
}

// 06 런 상태 FSM과 화면 흐름: 첫 화면 → 로비 → 런(층 진행) → 결과창 → 정산 → 로비.
// 층 전환(03 §6.2~6.3), 순간이동(08 §5.1), 보스 조우(05 §5.1), 사망/포기/엔딩(06 §5.5),
// 영구 재화 정산(06 §5.2), 저장 실패 처리(06 §5.4)를 담당함.
[DefaultExecutionOrder(-450)]
public class RunManager : MonoBehaviour
{
    public RunState State { get; private set; } = RunState.Boot;
    public int FloorIndex { get; private set; }
    public int RunSeed { get; private set; }
    public RunPlan Plan { get; private set; }
    public float RunTime { get; private set; }
    public int RoomsCleared { get; private set; }
    public int FloorBonusTotal { get; private set; }
    public int BundleTotal { get; private set; }
    public string SettlementId { get; private set; }
    public bool IsTransitioning { get; private set; }
    public bool IsEndingSequence { get; private set; }
    public bool IsResultScreen => State == RunState.RunCleared || State == RunState.RunOver;
    public bool InRunAndPlayable => State == RunState.InRun && !IsTransitioning && !IsEndingSequence;
    public int PendingPermanentCurrency => RoomsCleared * Meta.PermanentPerRoomCleared + FloorBonusTotal + BundleTotal;
    public PermanentData UnsavedSnapshot { get; private set; }

    [Tooltip("0이 아니면 이 시드로 런 시작 (테스트용)")]
    [SerializeField] private int fixedRunSeed;
    [Tooltip("테스트용: 첫 화면/로비를 건너뛰고 바로 런 시작")]
    [SerializeField] private bool skipMenusForTesting;

    private readonly List<string> runProgressUnlocks = new List<string>();
    private bool runTimeActive;
    private bool onboardingVisible;

    private MetaBalance Meta => GameBalance.Current.Meta;
    private UIManager UI => GameServices.UI;
    private GameConfig Config => GameServices.Config;

    private void Awake() => GameServices.Run = this;

    private void OnEnable()
    {
        GameEvents.PlayerDied += OnPlayerDied;
        GameEvents.CombatRoomCleared += OnCombatRoomCleared;
        GameEvents.CorridorEntered += OnCorridorEntered;
    }

    private void OnDisable()
    {
        GameEvents.PlayerDied -= OnPlayerDied;
        GameEvents.CombatRoomCleared -= OnCombatRoomCleared;
        GameEvents.CorridorEntered -= OnCorridorEntered;
    }

    private void Start() => StartCoroutine(Boot());

    private void Update()
    {
        // RunTime: 조작 가능 시점부터, 게임 전체 정지 구간 제외 (06 §5.1 3번). 히트스톱과 비정지 모달은 포함
        if (runTimeActive && !GameClock.IsPaused)
            RunTime += Time.unscaledDeltaTime;
    }

    // ---------------- 부팅 ----------------

    private IEnumerator Boot()
    {
        GameClock.Pause(PauseReason.OutOfRun);
        UI.SetBlack(true);
        GameServices.Save.Load();
        GameEvents.Raise(GameEvents.SaveLoaded);
        MetaProgression.ApplyPermanentStats();
        ApplySettings(GameServices.Save.Settings);
        yield return null;

        // 새 세이브(최초 실행, 손상 초기화)는 첫 화면 전에 즉시 저장. 실패하면 검은 화면 위 저장 실패 팝업
        if (GameServices.Save.IsNewSave)
        {
            bool done = false;
            SaveWithRecovery(GameServices.Save.Data, onSuccess: () => done = true, onGaveUp: () => done = true, blockingInRun: false);
            while (!done) yield return null;
        }

        UI.SetBlack(false);
        if (skipMenusForTesting)
        {
            StartRun();
            yield break;
        }
        GoToFirstScreen();
        if (GameServices.Save.WasCorruptedThisLaunch)
            UI.ShowSystemNotice("저장 데이터를 읽지 못해 새로 시작합니다", "확인", null);
    }

    public void ApplySettings(SettingsData settings)
    {
        AudioListener.volume = Mathf.Clamp01(settings.MasterVolume / 100f);
        GameServices.Audio?.SetVolumes(settings.BgmVolume, settings.SfxVolume);
        QualitySettings.vSyncCount = settings.VSync ? 1 : 0;
        if (GameServices.Camera != null) GameServices.Camera.ShakeStrength01 = Mathf.Clamp01(settings.ScreenShake / 100f);
    }

    // ---------------- 메뉴 ----------------

    public void GoToFirstScreen()
    {
        State = RunState.FirstScreen;
        GameClock.ResetAll();
        GameClock.Pause(PauseReason.OutOfRun);
        runTimeActive = false;
        GameServices.Floor?.ClearFloor();
        GameServices.Feedback?.ClearAll();
        SetPlayerActive(false);
        UI.ShowFirstScreen(OnFirstScreenStart, OnFirstScreenQuit);
    }

    // 미저장 스냅샷이 있으면 저장에 성공할 때만 로비로 (06 §5.4 8번)
    private void OnFirstScreenStart()
    {
        if (UnsavedSnapshot != null)
        {
            if (GameServices.Save.TrySave(UnsavedSnapshot))
            {
                UnsavedSnapshot = null;
                GoToLobby();
            }
            else
            {
                UI.ShowSaveFailure(second: true, onRetry: null, onFirstScreen: () => { });
            }
            return;
        }
        GoToLobby();
    }

    private void OnFirstScreenQuit()
    {
        if (UnsavedSnapshot != null)
        {
            UI.ShowConfirm("게임 종료", "저장하지 못한 진행이 사라집니다. 종료할까요?", Quit, null);
            return;
        }
        Quit();
    }

    private static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void GoToLobby()
    {
        State = RunState.Lobby;
        GameClock.ResetAll();
        GameClock.Pause(PauseReason.OutOfRun);
        GameServices.Floor?.ClearFloor();
        SetPlayerActive(false);
        UI.ShowLobby(StartRun, () => UI.ShowPermanentShop(), () => UI.ShowCodex(), GoToFirstScreen);

        // 결과창 전에 끝나 공개 못 한 진행 해금 카드가 있으면 먼저 표시 (06 §5.3 4b)
        List<string> unrevealed = MetaProgression.UnrevealedPendingUnlocks();
        if (unrevealed.Count > 0)
        {
            UI.ShowProgressCards(ToDefinitions(unrevealed), () =>
            {
                MetaProgression.MarkRevealed(unrevealed);
                SaveWithRecovery(GameServices.Save.Data, null, null, blockingInRun: false);
            });
        }
    }

    // ---------------- 런 시작 ----------------

    public void StartRun()
    {
        if (State == RunState.InRun) return;
        StartCoroutine(StartRunRoutine());
    }

    private IEnumerator StartRunRoutine()
    {
        IsTransitioning = true;
        UI.HideMenus();
        yield return UI.FadeOut(Meta.RunEntryFadeOut);

        RunSeed = fixedRunSeed != 0 ? fixedRunSeed : Environment.TickCount;
        RandomStreams.Begin(RunSeed);
        Plan = new RunPlan(RunSeed, Config);
        Debug.Log($"런 시작. 런 시드 {RunSeed}");

        FloorIndex = 0;
        RunTime = 0f;
        RoomsCleared = 0;
        FloorBonusTotal = 0;
        BundleTotal = 0;
        SettlementId = Guid.NewGuid().ToString("N");
        runProgressUnlocks.Clear();

        SetPlayerActive(true);
        PlayerController player = GameServices.Player;
        // 영구 강화(최대 HP 포함)를 먼저 등록해야 풀피가 새 최대치로 채워짐 (06-1 §5.3)
        MetaProgression.ApplyPermanentStats();
        player.ResetForNewRun();
        GameServices.Wallet.ResetForNewRun();
        GameServices.Artifacts.BeginRun(MetaProgression.UnlockedArtifactIds());
        GameEvents.Raise(GameEvents.RunStarted);

        State = RunState.InRun;
        GameClock.ResetAll();
        GameClock.Pause(PauseReason.Transition);
        float waitStart = Time.unscaledTime;
        if (!BuildAndPlace(0))
        {
            UI.ShowSystemNotice("층 생성 실패. 콘솔 오류를 확인하세요 (방 프리팹/설정)", "첫 화면으로", GoToFirstScreen);
            yield break;
        }
        while (Time.unscaledTime - waitStart < 0.01f) yield return null;

        UI.ShowHUD(true);
        yield return UI.FadeIn(Meta.RunEntryFadeIn);
        GameClock.Resume(PauseReason.Transition);
        IsTransitioning = false;
        runTimeActive = true;
        OnFloorReached();

        // 새 세이브 첫 런: 비차단 온보딩 (08 §5.3)
        if (!GameServices.Save.Data.OnboardingComplete)
        {
            onboardingVisible = true;
            UI.ShowOnboarding(true);
        }
    }

    private bool BuildAndPlace(int floorIndex)
    {
        FloorDirector floor = GameServices.Floor;
        if (!floor.BuildFloor(RunSeed, floorIndex, Plan)) return false;
        RoomRuntime start = floor.StartRoom;
        Vector2 spawn = start.TeleportArrival;
        GameServices.Player.PlaceAt(spawn);
        GameServices.Camera?.EnterRoom(start, catchUp: false);
        GameServices.Camera?.SnapTo(spawn);
        return true;
    }

    private void SetPlayerActive(bool active)
    {
        PlayerController player = GameServices.Player;
        if (player != null) player.gameObject.SetActive(active);
        UI?.ShowHUD(active && State == RunState.InRun);
    }

    // FloorReached: 입력이 다시 켜지는 순간 (03 §5). 최고 도달 층과 진행 조건 갱신 + 즉시 저장 (06 §5.4 5번)
    private void OnFloorReached()
    {
        int floorNumber = FloorIndex + 1;
        GameEvents.Raise(GameEvents.FloorReached, FloorIndex);
        string[] names = GameBalance.Current.FloorEnd.FloorNames;
        FloorContent content = Config != null ? Config.GetFloor(FloorIndex) : null;
        string floorName = content != null && !string.IsNullOrEmpty(content.DisplayName) ? content.DisplayName
            : names != null && FloorIndex < names.Length ? names[FloorIndex] : $"{floorNumber}층";
        UI.ShowFloorArrival(floorNumber, floorName);

        PermanentData data = GameServices.Save.Data;
        bool changed = false;
        if (floorNumber > data.HighestFloorReached)
        {
            data.HighestFloorReached = floorNumber;
            changed = true;
        }
        if (floorNumber == 2) { runProgressUnlocks.AddRange(MetaProgression.AchieveCondition(ProgressCondition.ReachFloor2)); changed = true; }
        if (floorNumber == 3) { runProgressUnlocks.AddRange(MetaProgression.AchieveCondition(ProgressCondition.ReachFloor3)); changed = true; }
        if (changed) SaveWithRecovery(data, null, null, blockingInRun: true);
    }

    private void OnCorridorEntered()
    {
        // 온보딩은 최초 CorridorEntered에 숨기고 완료 플래그 저장 (06 §5.4 6번)
        if (!onboardingVisible) return;
        onboardingVisible = false;
        UI.ShowOnboarding(false);
        GameServices.Save.Data.OnboardingComplete = true;
        SaveWithRecovery(GameServices.Save.Data, null, null, blockingInRun: true);
    }

    private void OnCombatRoomCleared(RoomRuntime room) => RoomsCleared++;

    public void AddPendingPermanentCurrency(int amount) => BundleTotal += amount;

    // 도감 발견 등록: 새 ID면 즉시 저장 (06 §5.4 4번)
    public void Discover(ArtifactDefinition def)
    {
        if (MetaProgression.MarkDiscovered(def))
            SaveWithRecovery(GameServices.Save.Data, null, null, blockingInRun: State == RunState.InRun);
    }

    // ---------------- 층 전환 ----------------

    // FloorExitRoom 이동 오브젝트 F (03-6 §6.1 7번). 확인 없이 다음 층
    public void ExitFloor()
    {
        if (!InRunAndPlayable) return;
        StartCoroutine(FloorTransitionRoutine());
    }

    private IEnumerator FloorTransitionRoutine()
    {
        IsTransitioning = true;
        int leaving = FloorIndex;
        GameEvents.Raise(GameEvents.FloorExited, leaving);
        int[] bonus = Meta.FloorClearBonus;
        FloorBonusTotal += bonus != null && leaving < bonus.Length ? bonus[leaving] : 0;

        UI.CloseAllModals();
        GameClock.Pause(PauseReason.Transition);
        GameServices.Camera?.SetTransitioning(true);
        yield return UI.FadeOut(GameBalance.Current.Camera.TransitionFadeOut);

        float blackStart = Time.unscaledTime;
        GameServices.Artifacts.OnFloorLeft();
        FloorIndex = leaving + 1;
        // 미리 계산이 안 끝났으면 검은 화면 유지. 1초를 넘으면 대기 표시 (03 §6.3 2~3번)
        while (!GameServices.Floor.IsFloorReady(RunSeed, FloorIndex))
        {
            if (Time.unscaledTime - blackStart > GameBalance.Current.FloorEnd.WaitIndicatorDelay) UI.ShowWaitIndicator(true);
            yield return null;
        }
        bool ok = BuildAndPlace(FloorIndex);
        if (Time.unscaledTime - blackStart > GameBalance.Current.FloorEnd.WaitIndicatorDelay) UI.ShowWaitIndicator(true);
        yield return null;
        UI.ShowWaitIndicator(false);
        if (!ok)
        {
            UI.ShowSystemNotice("층 생성 실패. 콘솔 오류를 확인하세요", "첫 화면으로", GoToFirstScreen);
            yield break;
        }

        GameServices.Player.RefreshForNewFloor();
        yield return UI.FadeIn(GameBalance.Current.Camera.TransitionFadeIn);
        GameServices.Camera?.SetTransitioning(false);
        GameClock.Resume(PauseReason.Transition);
        IsTransitioning = false;
        OnFloorReached();
    }

    // ---------------- 순간이동 ----------------

    public bool CanTeleport(RoomRuntime target)
    {
        if (!InRunAndPlayable || target == null || !target.Visited) return false;
        RoomRuntime current = GameServices.Floor.CurrentRoom;
        if (current != null && current.BlocksModal && !GameServices.Floor.InCorridor) return false;
        if (GameServices.Player.Movement.IsDodging) return false;
        return true;
    }

    public void TeleportTo(RoomRuntime target)
    {
        if (!CanTeleport(target)) return;
        StartCoroutine(TeleportRoutine(target));
    }

    private IEnumerator TeleportRoutine(RoomRuntime target)
    {
        IsTransitioning = true;
        UI.CloseAllModals();
        GameClock.Pause(PauseReason.Transition);
        GameServices.Camera?.SetTransitioning(true);
        yield return UI.FadeOut(GameBalance.Current.Camera.TransitionFadeOut);
        Vector2 arrival = target.TeleportArrival;
        GameServices.Player.PlaceAt(arrival);
        GameServices.Floor.TeleportInto(target);
        GameServices.Camera?.SnapTo(arrival);
        Projectile.ClearAll();
        yield return UI.FadeIn(GameBalance.Current.Camera.TransitionFadeIn);
        GameServices.Camera?.SetTransitioning(false);
        GameClock.Resume(PauseReason.Transition);
        IsTransitioning = false;
    }

    // ---------------- 보스 조우 ----------------

    public void BeginBossEncounter(FinalBossRoomController room)
    {
        StartCoroutine(BossEncounterRoutine(room));
    }

    // 첫 RoomEntered → 페이드 아웃 중 잠금/배치 → 페이드 인 → 등장 연출 → Combat + 입력 재개 (05 §5.1)
    private IEnumerator BossEncounterRoutine(FinalBossRoomController room)
    {
        IsTransitioning = true;
        GameClock.Pause(PauseReason.BossIntro);
        GameServices.Camera?.SetTransitioning(true);
        yield return UI.FadeOut(GameBalance.Current.Camera.TransitionFadeOut);
        room.LockAndPlaceBoss();
        GameServices.Camera?.EnterRoom(room.Runtime, catchUp: false);
        GameServices.Camera?.SnapTo(GameServices.Player.Position);
        GameServices.Camera?.SetTransitioning(false);
        yield return UI.FadeIn(GameBalance.Current.Camera.TransitionFadeIn);
        if (room.Boss != null) UI.ShowBossBar(room.Boss);
        UI.ShowCenterToast(room.Boss != null ? room.Boss.Definition.DisplayName : "최종보스", GameBalance.Current.Boss.IntroDuration);
        yield return new WaitForSecondsRealtime(GameBalance.Current.Boss.IntroDuration);
        room.Boss?.StartEncounter();
        GameClock.Resume(PauseReason.BossIntro);
        IsTransitioning = false;
    }

    // ---------------- 런 종료 ----------------

    // 사망: 같은 프레임 조작/몬스터/투사체 정지 → 사망 효과 + 검은 페이드 → 게임오버 결과창 (06 §5.5)
    private void OnPlayerDied()
    {
        if (State != RunState.InRun) return;
        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        GameClock.Pause(PauseReason.Death);
        runTimeActive = false;
        State = RunState.RunOver;
        GameEvents.Raise(GameEvents.RunOver);
        UI.CloseAllModals();
        float total = Meta.DeathResultTransitionDuration;
        GameServices.Feedback?.SpawnRewardBurst(GameServices.Player.Position, Color.red);
        yield return new WaitForSecondsRealtime(total * 0.5f);
        yield return UI.FadeOut(total * 0.5f);
        EndRunCleanup();
        ShowResult(cleared: false);
    }

    // 일시정지 메뉴 런 포기 확정 → 사망 효과 없이 검은 페이드 → RunOver 결과창
    public void AbandonRun()
    {
        if (State != RunState.InRun) return;
        StartCoroutine(AbandonRoutine());
    }

    private IEnumerator AbandonRoutine()
    {
        runTimeActive = false;
        State = RunState.RunOver;
        GameEvents.Raise(GameEvents.RunOver);
        UI.CloseAllModals();
        GameClock.Pause(PauseReason.Death);
        yield return UI.FadeOut(Meta.AbandonResultFadeDuration);
        EndRunCleanup();
        ShowResult(cleared: false);
    }

    // 엔딩 상자 F → EndingTriggered + RunCleared, 입력 차단과 월드 정지, 검은 페이드, 엔딩, 결과창 (06 §5.5)
    public void TriggerEnding()
    {
        if (!InRunAndPlayable) return;
        StartCoroutine(EndingRoutine());
    }

    private IEnumerator EndingRoutine()
    {
        IsEndingSequence = true;
        GameClock.Pause(PauseReason.Ending);
        runTimeActive = false;
        GameEvents.Raise(GameEvents.EndingTriggered);
        int[] bonus = Meta.FloorClearBonus;
        FloorBonusTotal += bonus != null && FloorIndex < bonus.Length ? bonus[FloorIndex] : 0;

        State = RunState.RunCleared;
        GameEvents.Raise(GameEvents.RunCleared);
        PermanentData data = GameServices.Save.Data;
        data.RunClearedCount++;
        runProgressUnlocks.AddRange(MetaProgression.AchieveCondition(ProgressCondition.FirstClear));
        if (data.RunClearedCount >= 2) runProgressUnlocks.AddRange(MetaProgression.AchieveCondition(ProgressCondition.SecondClear));
        bool saveGaveUp = false;
        SaveWithRecovery(data, null, () => saveGaveUp = true, blockingInRun: true);
        while (GameClock.IsPausedBy(PauseReason.SaveFailure)) yield return null;
        if (saveGaveUp) yield break;

        UI.CloseAllModals();
        UI.ShowHUD(false);
        // 누적 2회 이상이면 ESC로 남은 페이드와 엔딩 건너뛰기, 첫 클리어는 모든 입력 무시
        bool skippable = data.RunClearedCount >= 2;
        bool skipped = false;
        float fade = Meta.AbandonResultFadeDuration;
        float t = 0f;
        while (t < fade)
        {
            if (skippable && UI.EndingSkipPressed()) { skipped = true; break; }
            t += Time.unscaledDeltaTime;
            UI.SetFade(t / fade);
            yield return null;
        }
        UI.SetFade(1f);
        if (!skipped)
        {
            UI.ShowEnding(Meta.EndingLastLine);
            float end = Time.unscaledTime + Meta.EndingDuration;
            while (Time.unscaledTime < end)
            {
                if (skippable && UI.EndingSkipPressed()) break;
                yield return null;
            }
        }
        UI.HideEnding();
        IsEndingSequence = false;
        EndRunCleanup();
        ShowResult(cleared: true);
    }

    private void EndRunCleanup()
    {
        GameServices.Wallet.LoseAll();
        GameServices.Player.Stats.ClearRunSources();
        MetaProgression.ApplyPermanentStats();
        UI.ShowHUD(false);
        UI.ShowOnboarding(false);
        onboardingVisible = false;
    }

    private void ShowResult(bool cleared)
    {
        GameServices.Floor.TrackingEnabled = false;
        UI.SetFade(0f);
        var info = new ResultInfo
        {
            Cleared = cleared,
            FloorReached = FloorIndex + 1,
            RoomsCleared = RoomsCleared,
            RunTime = RunTime,
            PermanentCurrency = PendingPermanentCurrency,
        };
        List<string> cards = new List<string>(runProgressUnlocks);
        UI.ShowResult(info, ToDefinitions(cards), OnResultToLobby);
        // 결과창 진행 해금 카드: 표시 순간 발견 + 공개 완료 즉시 저장 (06 §5.3 5, 6번)
        if (cards.Count > 0)
        {
            MetaProgression.MarkRevealed(cards);
            SaveWithRecovery(GameServices.Save.Data, null, null, blockingInRun: false);
        }
    }

    // 로비로: 같은 정산 ID는 한 번만 반영 → 저장 성공 뒤 로비 (06 §5.2 4, 5번)
    private void OnResultToLobby()
    {
        PermanentData data = GameServices.Save.Data;
        if (!data.AppliedSettlementIds.Contains(SettlementId))
        {
            data.AppliedSettlementIds.Add(SettlementId);
            if (data.AppliedSettlementIds.Count > 20) data.AppliedSettlementIds.RemoveAt(0);
            data.PermanentCurrency += PendingPermanentCurrency;
            GameEvents.Raise(GameEvents.PermanentCurrencyGranted, PendingPermanentCurrency);
        }
        SaveWithRecovery(data, onSuccess: () =>
        {
            GameServices.Floor.TrackingEnabled = true;
            GoToLobby();
        }, onGaveUp: null, blockingInRun: false);
    }

    // ---------------- 저장 실패 처리 (06 §5.4 7, 8번) ----------------

    // 저장 → 실패하면 차단 팝업(재시도 1회) → 또 실패하면 첫 화면으로, 스냅샷은 메모리에 남김
    public void SaveWithRecovery(PermanentData data, Action onSuccess, Action onGaveUp, bool blockingInRun)
    {
        if (GameServices.Save.TrySave(data))
        {
            onSuccess?.Invoke();
            return;
        }
        GameClock.Pause(PauseReason.SaveFailure);
        UI.ShowSaveFailure(second: false, onRetry: () =>
        {
            if (GameServices.Save.TrySave(data))
            {
                GameClock.Resume(PauseReason.SaveFailure);
                onSuccess?.Invoke();
                return true;
            }
            return false;
        }, onFirstScreen: () =>
        {
            UnsavedSnapshot = data.Clone();
            GameClock.Resume(PauseReason.SaveFailure);
            onGaveUp?.Invoke();
            StopAllCoroutines();
            IsTransitioning = false;
            IsEndingSequence = false;
            UI.CloseAllModals();
            UI.HideEnding();
            UI.SetFade(0f);
            GoToFirstScreen();
        });
    }

    private List<ArtifactDefinition> ToDefinitions(IEnumerable<string> ids)
    {
        var list = new List<ArtifactDefinition>();
        ArtifactCatalog catalog = Config != null ? Config.Artifacts : null;
        if (catalog == null) return list;
        foreach (string id in ids)
        {
            ArtifactDefinition def = catalog.Find(id);
            if (def != null) list.Add(def);
        }
        return list;
    }
}

public struct ResultInfo
{
    public bool Cleared;
    public int FloorReached;
    public int RoomsCleared;
    public float RunTime;
    public int PermanentCurrency;
}
