using UnityEngine;

public class PauseMenuController : MonoBehaviour
{
    [SerializeField] private PlayerInputReader input;
    [SerializeField] private PlayerMovement movement;
    [SerializeField] private GameObject panel;

    public bool IsOpen { get; private set; }

    private void OnEnable() => input.PausePressed += OnPausePressed;
    private void OnDisable() => input.PausePressed -= OnPausePressed;

    private void OnPausePressed()
    {
        if (IsOpen)
        {
            Resume();
            return;
        }

        if (ModalGate.AnyModalOpen) return;
        if (movement.CurrentState == movement.DodgeState) return;

        Open();
    }

    public void Open()
    {
        IsOpen = true;
        panel.SetActive(true);
        Time.timeScale = 0f;
        ModalGate.Register(true);
    }

    public void Resume()
    {
        IsOpen = false;
        panel.SetActive(false);
        Time.timeScale = 1f;
        ModalGate.Register(false);
    }

    public void QuitGame()
    {
        Debug.Log("[Pause] 프로그램 종료");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void AbandonRun()
    {
        Debug.Log("[Pause] 런 포기 확정");
        PlaytestLogger.FinalizeRun("RunOver");
        RunCurrency.Reset();
        RunProgressState.Reset();
        IsOpen = false;
        panel.SetActive(false);
        ModalGate.Register(false);
        FindObjectOfType<ResultScreenController>()?.ShowOver();
    }
}