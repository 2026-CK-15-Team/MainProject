using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ArtifactAcquireUI : MonoBehaviour
{
    [SerializeField] private PlayerInputReader input;
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text infoText;
    [SerializeField] private Button rerollButton;
    [SerializeField] private TMP_Text rerollButtonLabel;

    private FieldArtifact currentSource;

    public bool IsOpen => panel.activeSelf;

    private void OnEnable() => input.PausePressed += OnEscapePressed;
    private void OnDisable() => input.PausePressed -= OnEscapePressed;

    public void Open(FieldArtifact source)
    {
        currentSource = source;
        panel.SetActive(true);
        Time.timeScale = 0f;
        ModalGate.Register(true);
        Refresh();
    }

    private void Refresh()
    {
        var def = currentSource.CurrentDefinition;
        infoText.text = $"{def.DisplayName}\n{def.Grade} / {def.SetType}";

        bool canReroll = currentSource.CanReroll;
        rerollButton.gameObject.SetActive(canReroll);
        if (canReroll)
            rerollButtonLabel.text = currentSource.RerollCount == 0 ? "Reroll - Free" : "Reroll - 6";
    }

    public void OnRerollClicked()
    {
        if (currentSource.TryReroll(out _))
            Refresh();
    }

    public void OnKeepClicked()
    {
        currentSource.ConfirmKeep();
        Close();
    }

    public void OnDiscardClicked()
    {
        currentSource.ConfirmDiscard();
        Close();
    }

    private void OnEscapePressed()
    {
        if (!IsOpen) return;
        Close(); // 보류 처리, 유지/버리기 아님 - 상태는 FieldArtifact에 그대로 남음
    }

    private void Close()
    {
        panel.SetActive(false);
        Time.timeScale = 1f;
        currentSource = null;
        ModalGate.Register(false);
    }
}