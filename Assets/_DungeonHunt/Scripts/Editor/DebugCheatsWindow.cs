using UnityEditor;
using UnityEngine;

// 플레이 중 테스트용 창: DungeonHunt/Debug Cheats
public class DebugCheatsWindow : EditorWindow
{
    [MenuItem("DungeonHunt/Debug Cheats", priority = 80)]
    public static void Open() => GetWindow<DebugCheatsWindow>("DH Cheats");

    private void OnGUI()
    {
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("플레이 중에만 동작", MessageType.Info);
            return;
        }
        PlayerController player = GameServices.Player;
        RunManager run = GameServices.Run;
        EditorGUILayout.LabelField("런 상태", run != null ? run.State.ToString() : "-");
        if (run != null) EditorGUILayout.LabelField("런 시드", run.RunSeed.ToString());
        if (GameServices.Floor != null) EditorGUILayout.LabelField("층", (GameServices.Floor.FloorIndex + 1).ToString());

        if (player != null)
        {
            if (GUILayout.Button("HP 회복")) player.Health.Heal(99);
            if (GUILayout.Button("재화 +50")) GameServices.Wallet?.Add(50);
        }
        if (GUILayout.Button("현재 룸 몬스터 전부 처치"))
        {
            foreach (Monster m in new System.Collections.Generic.List<Monster>(Monster.Alive))
                m.TakeDamage(new DamageInfo { Amount = 99999, Source = DamageSource.Player, AttackExecutionId = -1 });
        }
        if (GUILayout.Button("다음 층으로")) run?.ExitFloor();
        if (GUILayout.Button("영구 재화 +100") && GameServices.Save != null)
        {
            GameServices.Save.Data.PermanentCurrency += 100;
            GameServices.Save.TrySave();
        }
        if (GameServices.Save != null)
        {
            if (GUILayout.Button("저장 실패 흉내 켜기")) GameServices.Save.SetSimulateFailure(true);
            if (GUILayout.Button("저장 실패 흉내 끄기")) GameServices.Save.SetSimulateFailure(false);
        }
    }

    private void OnInspectorUpdate() => Repaint();
}
