using System;
using System.IO;
using UnityEngine;

// 06 §5.4 세이브. 저장 슬롯 하나, JSON, 원자적 쓰기(임시 파일 → 교체).
// - 파일 없음 → 새 세이브 만들고 첫 화면 전에 즉시 저장
// - 손상 → 새 세이브로 초기화하고 즉시 저장 + 이번 실행에만 초기화 안내
// - 쓰기 실패 → 호출 쪽(RunManager)이 차단 팝업과 재시도 흐름 처리
public class SaveService : MonoBehaviour
{
    private const string FileName = "permanent.json";
    private const string SettingsFileName = "settings.json";

    public PermanentData Data { get; private set; } = new PermanentData();
    public SettingsData Settings { get; private set; } = new SettingsData();
    public bool WasCorruptedThisLaunch { get; private set; }
    public bool IsNewSave { get; private set; }

    // 테스트용: 강제로 저장 실패시키기
    [SerializeField] private bool simulateSaveFailure;

    private string SavePath => Path.Combine(Application.persistentDataPath, FileName);
    private string SettingsPath => Path.Combine(Application.persistentDataPath, SettingsFileName);

    private void Awake() => GameServices.Save = this;

    public void Load()
    {
        WasCorruptedThisLaunch = false;
        IsNewSave = false;
        if (!File.Exists(SavePath))
        {
            Data = new PermanentData();
            IsNewSave = true;
        }
        else
        {
            try
            {
                string json = File.ReadAllText(SavePath);
                PermanentData loaded = JsonUtility.FromJson<PermanentData>(json);
                if (loaded == null || loaded.Version <= 0) throw new Exception("빈 세이브");
                Data = loaded;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"세이브 손상, 새로 시작: {e.Message}");
                Data = new PermanentData();
                WasCorruptedThisLaunch = true;
                IsNewSave = true;
            }
        }
        LoadSettings();
    }

    // 성공하면 true + GameSaved
    public bool TrySave() => TrySave(Data);

    public bool TrySave(PermanentData snapshot)
    {
        if (simulateSaveFailure) return false;
        try
        {
            string json = JsonUtility.ToJson(snapshot, true);
            string temp = SavePath + ".tmp";
            Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
            File.WriteAllText(temp, json);
            if (File.Exists(SavePath)) File.Replace(temp, SavePath, null);
            else File.Move(temp, SavePath);
            GameEvents.Raise(GameEvents.GameSaved);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"저장 실패: {e.Message}");
            return false;
        }
    }

    public void SetSimulateFailure(bool value) => simulateSaveFailure = value;

    public void DeleteSave()
    {
        try { if (File.Exists(SavePath)) File.Delete(SavePath); } catch (Exception e) { Debug.LogError(e.Message); }
        Data = new PermanentData();
    }

    // ---------- 설정 ----------

    private void LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsPath))
                Settings = JsonUtility.FromJson<SettingsData>(File.ReadAllText(SettingsPath)) ?? new SettingsData();
            else
                Settings = new SettingsData { ScreenShake = GameBalance.Current.UI.ScreenShakeDefault };
        }
        catch
        {
            Settings = new SettingsData();
        }
    }

    public bool TrySaveSettings(SettingsData candidate)
    {
        if (simulateSaveFailure) return false;
        try
        {
            File.WriteAllText(SettingsPath, JsonUtility.ToJson(candidate, true));
            Settings = candidate;
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"설정 저장 실패: {e.Message}");
            return false;
        }
    }
}
