using System;
using UnityEngine;
using UnityEngine.UI;

// 시스템 팝업 틀: 저장 실패(08 §8.4), 손상 세이브 안내(08 §8.6), 확인창. 버튼으로만 닫는 팝업은 ESC 무시
public class SystemPopupView : UIView
{
    private Text titleText, messageText;
    private Button primary, secondary;
    private Action onPrimary, onSecondary;
    private bool escCancels;
    public override ModalKind Kind => ModalKind.System;
    public override bool ClosesOnEsc => escCancels;

    protected override void Build(RectTransform root)
    {
        RectTransform panel = CenterPanel(root, new Vector2(900f, 420f));
        titleText = Title(panel, "");
        messageText = UIFactory.Label("Message", panel, "", 30, TextAnchor.MiddleCenter);
        UIFactory.Place(messageText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(820f, 180f));
        primary = UIFactory.Button("Primary", panel, "확인", () => Press(true));
        secondary = UIFactory.Button("Secondary", panel, "취소", () => Press(false));
    }

    public void Setup(string title, string message, string primaryText, Action primaryAction, string secondaryText = null, Action secondaryAction = null, bool escCancelsPopup = false)
    {
        titleText.text = title;
        messageText.text = message;
        onPrimary = primaryAction;
        onSecondary = secondaryAction;
        escCancels = escCancelsPopup;
        UIFactory.SetButtonText(primary, primaryText);
        bool two = !string.IsNullOrEmpty(secondaryText);
        secondary.gameObject.SetActive(two);
        if (two) UIFactory.SetButtonText(secondary, secondaryText);
        UIFactory.Place((RectTransform)primary.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(two ? -170f : 0f, 40f), new Vector2(300f, 70f));
        UIFactory.Place((RectTransform)secondary.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(170f, 40f), new Vector2(300f, 70f));
    }

    private void Press(bool isPrimary)
    {
        Action action = isPrimary ? onPrimary : onSecondary;
        Manager.CloseView(this);
        action?.Invoke();
    }

    public override void OnEscape()
    {
        if (escCancels) Press(false);
    }
}

// 저장 실패 팝업: 첫 실패는 재시도 하나, 재시도도 실패하면 첫 화면으로 하나 (08 §8.4). ESC 무시
public class SaveFailureView : UIView
{
    private Text messageText;
    private Button button;
    private Func<bool> retry;
    private Action toFirstScreen;
    private bool secondStage;
    public override ModalKind Kind => ModalKind.System;
    public override bool ClosesOnEsc => false;

    protected override void Build(RectTransform root)
    {
        RectTransform panel = CenterPanel(root, new Vector2(900f, 400f));
        Title(panel, "저장 실패");
        messageText = UIFactory.Label("Message", panel, "", 30, TextAnchor.MiddleCenter);
        UIFactory.Place(messageText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(820f, 160f));
        button = UIFactory.Button("Button", panel, "재시도", OnPressed);
        UIFactory.Place((RectTransform)button.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(320f, 70f));
    }

    public void Setup(bool second, Func<bool> onRetry, Action onFirstScreen)
    {
        retry = onRetry;
        toFirstScreen = onFirstScreen;
        SetStage(second || onRetry == null);
    }

    private void SetStage(bool second)
    {
        secondStage = second;
        messageText.text = second ? "저장하지 못했습니다" : "진행을 저장하지 못했습니다. 다시 시도합니다";
        UIFactory.SetButtonText(button, second ? "첫 화면으로" : "재시도");
    }

    private void OnPressed()
    {
        if (secondStage)
        {
            Manager.CloseView(this);
            toFirstScreen?.Invoke();
            return;
        }
        if (retry != null && retry())
        {
            Manager.CloseView(this);
            return;
        }
        SetStage(true);
    }

    public override void OnEscape() { }
}

// 포커스 복귀 오버레이 (08 §8.3): 자동 재개 없음, 계속하기를 눌러야 복귀. ESC 무시
public class FocusOverlayView : UIView
{
    public override ModalKind Kind => ModalKind.System;
    public override bool ClosesOnEsc => false;

    protected override void Build(RectTransform root)
    {
        RectTransform panel = CenterPanel(root, new Vector2(700f, 300f));
        Title(panel, "일시정지됨");
        Button resume = UIFactory.Button("Resume", panel, "계속하기", () => Manager.CloseView(this));
        UIFactory.Place((RectTransform)resume.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(320f, 70f));
    }

    protected override void OnOpened() => GameClock.Pause(PauseReason.Focus);
    protected override void OnClosed() => GameClock.Resume(PauseReason.Focus);
    public override void OnEscape() { }
}

// 일시정지 메뉴 (08 §5.4): 계속하기, 조작법, 설정, 런 포기. 게임 정지
public class PauseMenuView : UIView
{
    public override ModalKind Kind => ModalKind.TimeStop;

    protected override void Build(RectTransform root)
    {
        RectTransform panel = CenterPanel(root, new Vector2(600f, 620f));
        Title(panel, "일시정지");
        string[] labels = { "계속하기", "조작법", "설정", "런 포기" };
        Action[] actions =
        {
            () => Manager.CloseView(this),
            () => Manager.OpenControls(),
            () => Manager.OpenSettings(),
            () => Manager.ShowConfirm("런 포기", "런을 포기하면 결과창으로 이동합니다.", () => { Manager.CloseView(this); GameServices.Run.AbandonRun(); }, null),
        };
        for (int i = 0; i < labels.Length; i++)
        {
            Button b = UIFactory.Button(labels[i], panel, labels[i], actions[i], 32);
            UIFactory.Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -130f - i * 105f), new Vector2(420f, 80f));
        }
    }

    protected override void OnOpened() => GameClock.Pause(PauseReason.PauseMenu);
    protected override void OnClosed() => GameClock.Resume(PauseReason.PauseMenu);
}

// 조작법 재열람 (08 §5.3). ESC/뒤로 → 일시정지 메뉴
public class ControlsView : UIView
{
    public override ModalKind Kind => ModalKind.TimeStop;

    protected override void Build(RectTransform root)
    {
        RectTransform panel = CenterPanel(root, new Vector2(900f, 640f), dimBackground: false);
        Title(panel, "조작법");
        Text keys = UIFactory.Label("Keys", panel, OnboardingView.ControlsText, 32, TextAnchor.UpperLeft);
        UIFactory.Place(keys.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(700f, 400f));
        Button back = UIFactory.Button("Back", panel, "뒤로", () => Manager.CloseView(this));
        UIFactory.Place((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(300f, 70f));
    }
}

// 설정 (08 §5.4): 음량은 즉시 미리보기, 해상도/창 모드는 적용 때 반영 + 10초 유지 확인
public class SettingsView : UIView
{
    private SettingsData preview;
    private SettingsData lastConfirmed;
    private Text windowText, resolutionText, vsyncText;
    private Slider master, bgm, sfx, shake;
    private float keepDeadline = -1f;
    private Text keepText;
    private GameObject keepPanel;

    public override ModalKind Kind => ModalKind.TimeStop;

    private static readonly string[] WindowModes = { "전체 화면", "테두리 없는 전체 화면", "창 모드" };

    protected override void Build(RectTransform root)
    {
        // 메뉴(첫 화면, 로비)에서 열 때 뒤 버튼이 눌리지 않게 막음. 일시정지 메뉴 위에서는 어둡게 겹치지 않도록 투명
        Image blocker = UIFactory.Panel("Blocker", root, new Color(0f, 0f, 0f, 0f));
        UIFactory.Stretch(blocker.rectTransform);
        RectTransform panel = CenterPanel(root, new Vector2(1000f, 860f), dimBackground: false);
        Title(panel, "설정");
        float y = -120f;
        master = Row(panel, "전체 음량", ref y, v => { preview.MasterVolume = v; ApplyPreview(); });
        bgm = Row(panel, "BGM 음량", ref y, v => { preview.BgmVolume = v; ApplyPreview(); });
        sfx = Row(panel, "효과음 음량", ref y, v => { preview.SfxVolume = v; ApplyPreview(); });
        shake = Row(panel, "화면 흔들림", ref y, v => { preview.ScreenShake = v; ApplyPreview(); });

        vsyncText = CycleRow(panel, "수직동기화", ref y, () => { preview.VSync = !preview.VSync; ApplyPreview(); RefreshTexts(); });
        windowText = CycleRow(panel, "창 모드", ref y, () => { preview.WindowMode = (preview.WindowMode + 1) % 3; RefreshTexts(); });
        resolutionText = CycleRow(panel, "해상도", ref y, CycleResolution);

        Button apply = UIFactory.Button("Apply", panel, "적용", Apply);
        UIFactory.Place((RectTransform)apply.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-170f, 30f), new Vector2(300f, 70f));
        Button back = UIFactory.Button("Back", panel, "뒤로", () => Manager.CloseView(this));
        UIFactory.Place((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(170f, 30f), new Vector2(300f, 70f));

        keepPanel = CenterPanel(UIFactory.CreateRect("Keep", root), new Vector2(700f, 300f)).parent.gameObject;
        RectTransform keep = (RectTransform)keepPanel.transform.Find("Panel");
        keepText = UIFactory.Label("KeepText", keep, "", 30, TextAnchor.UpperCenter);
        UIFactory.Place(keepText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(640f, 120f));
        Button keepButton = UIFactory.Button("KeepYes", keep, "유지", ConfirmKeep);
        UIFactory.Place((RectTransform)keepButton.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-150f, 30f), new Vector2(260f, 70f));
        Button revert = UIFactory.Button("Revert", keep, "되돌리기", Revert);
        UIFactory.Place((RectTransform)revert.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(150f, 30f), new Vector2(260f, 70f));
        UIFactory.Stretch((RectTransform)keepPanel.transform);
        keepPanel.SetActive(false);
    }

    private Slider Row(RectTransform panel, string label, ref float y, Action<float> onChanged)
    {
        Text text = UIFactory.Label(label, panel, label, 30, TextAnchor.MiddleLeft);
        UIFactory.Place(text.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f, y), new Vector2(300f, 50f));
        Slider slider = UIFactory.Slider(label + "Slider", panel, 0, 100, 50, onChanged);
        UIFactory.Place((RectTransform)slider.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(380f, y - 12f), new Vector2(540f, 26f));
        y -= 80f;
        return slider;
    }

    private Text CycleRow(RectTransform panel, string label, ref float y, Action onClick)
    {
        Text text = UIFactory.Label(label, panel, label, 30, TextAnchor.MiddleLeft);
        UIFactory.Place(text.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f, y), new Vector2(300f, 50f));
        Button button = UIFactory.Button(label + "Button", panel, "", onClick, 26);
        UIFactory.Place((RectTransform)button.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(380f, y), new Vector2(540f, 56f));
        y -= 80f;
        return button.GetComponentInChildren<Text>();
    }

    protected override void OnOpened()
    {
        lastConfirmed = Copy(GameServices.Save.Settings);
        preview = Copy(lastConfirmed);
        if (preview.ResolutionWidth <= 0) { preview.ResolutionWidth = Screen.width; preview.ResolutionHeight = Screen.height; }
        master.SetValueWithoutNotify(preview.MasterVolume);
        bgm.SetValueWithoutNotify(preview.BgmVolume);
        sfx.SetValueWithoutNotify(preview.SfxVolume);
        shake.SetValueWithoutNotify(preview.ScreenShake);
        RefreshTexts();
        keepPanel.SetActive(false);
        keepDeadline = -1f;
    }

    // ESC/뒤로: 미리보기 버리고 마지막 확정값 복구 (08 §5.4)
    protected override void OnClosed()
    {
        GameServices.Run.ApplySettings(lastConfirmed);
        keepPanel.SetActive(false);
        keepDeadline = -1f;
    }

    public override void OnEscape()
    {
        if (keepPanel.activeSelf) { Revert(); return; }
        Manager.CloseView(this);
    }

    private void ApplyPreview() => GameServices.Run.ApplySettings(preview);

    private void RefreshTexts()
    {
        vsyncText.text = preview.VSync ? "켜짐" : "꺼짐";
        windowText.text = WindowModes[Mathf.Clamp(preview.WindowMode, 0, 2)];
        resolutionText.text = $"{preview.ResolutionWidth} x {preview.ResolutionHeight}";
    }

    private void CycleResolution()
    {
        Resolution[] list = Screen.resolutions;
        if (list == null || list.Length == 0) return;
        int index = Array.FindIndex(list, r => r.width == preview.ResolutionWidth && r.height == preview.ResolutionHeight);
        Resolution next = list[(index + 1 + list.Length) % list.Length];
        preview.ResolutionWidth = next.width;
        preview.ResolutionHeight = next.height;
        RefreshTexts();
    }

    private bool DisplayChanged => preview.WindowMode != lastConfirmed.WindowMode
        || preview.ResolutionWidth != lastConfirmed.ResolutionWidth || preview.ResolutionHeight != lastConfirmed.ResolutionHeight;

    private void Apply()
    {
        if (DisplayChanged)
        {
            ApplyDisplay(preview);
            keepPanel.SetActive(true);
            keepPanel.transform.SetAsLastSibling();
            keepDeadline = Time.unscaledTime + 10f;
            return;
        }
        Save();
    }

    private void ConfirmKeep()
    {
        keepPanel.SetActive(false);
        keepDeadline = -1f;
        Save();
    }

    // 되돌리기, ESC, 시간 초과: 화면과 미리보기 모두 마지막 확정값으로
    private void Revert()
    {
        keepPanel.SetActive(false);
        keepDeadline = -1f;
        ApplyDisplay(lastConfirmed);
        preview = Copy(lastConfirmed);
        GameServices.Run.ApplySettings(lastConfirmed);
        OnOpened();
    }

    private void Save()
    {
        SettingsData candidate = Copy(preview);
        if (GameServices.Save.TrySaveSettings(candidate))
        {
            lastConfirmed = Copy(candidate);
            GameServices.Run.ApplySettings(candidate);
            return;
        }
        // 설정 저장 실패: 재시도 / 취소 (08 §8.5)
        Manager.ShowConfirm("설정 저장 실패", "설정을 저장하지 못했습니다.", Save, () =>
        {
            ApplyDisplay(lastConfirmed);
            GameServices.Run.ApplySettings(lastConfirmed);
            OnOpened();
        }, "재시도", "취소");
    }

    private static void ApplyDisplay(SettingsData data)
    {
        FullScreenMode mode = data.WindowMode switch
        {
            0 => FullScreenMode.ExclusiveFullScreen,
            1 => FullScreenMode.FullScreenWindow,
            _ => FullScreenMode.Windowed,
        };
        if (data.ResolutionWidth > 0 && data.ResolutionHeight > 0)
            Screen.SetResolution(data.ResolutionWidth, data.ResolutionHeight, mode);
        else
            Screen.fullScreenMode = mode;
    }

    public override void Tick()
    {
        if (keepDeadline > 0f)
        {
            float remaining = keepDeadline - Time.unscaledTime;
            keepText.text = $"이 화면 설정을 유지할까요?\n{Mathf.CeilToInt(Mathf.Max(0f, remaining))}초 뒤 되돌립니다";
            if (remaining <= 0f) Revert();
        }
    }

    private static SettingsData Copy(SettingsData source) => JsonUtility.FromJson<SettingsData>(JsonUtility.ToJson(source));
}
