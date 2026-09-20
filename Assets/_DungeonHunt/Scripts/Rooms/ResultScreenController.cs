using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ResultScreenController : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private TMP_Text floorText;
    [SerializeField] private TMP_Text clearedRoomsText;
    [SerializeField] private TMP_Text runTimeText;

    public void ShowCleared() => Show("RunCleared");
    public void ShowOver() => Show("RunOver");

    private void Show(string result)
    {
        Time.timeScale = 0f;
        panel.SetActive(true);
        ModalGate.Register(true);

        resultText.text = result;
        floorText.text = "Floor 1";
        clearedRoomsText.text = $"{RunStats.ClearedCombatRoomCount}";

        float runTime = RunStats.GetRunTime();
        int minutes = Mathf.FloorToInt(runTime / 60f);
        int seconds = Mathf.FloorToInt(runTime % 60f);
        runTimeText.text = $"{minutes:00}:{seconds:00}";
    }

    public void Restart()
    {
        PlaytestLogger.RetryFinalSave();
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void GoToLobby()
    {
        Restart();
    }
}