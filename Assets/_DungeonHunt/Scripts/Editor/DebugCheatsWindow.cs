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
        FinalBoss boss = FinalBoss.Current;
        if (boss != null && boss.State == BossState.Combat)
        {
            EditorGUILayout.LabelField("보스 HP", $"{boss.CurrentHP} / {boss.MaxHP}{(boss.IsLowHP ? " (후반)" : "")}");
            if (GUILayout.Button("보스 HP 50% 바로 아래로"))
                boss.TakeDamage(DamageInfo.Simple(Mathf.Max(1, boss.CurrentHP - Mathf.CeilToInt(boss.MaxHP * GameBalance.Current.Boss.LowHPThreshold) + 1), DamageSource.Player));
            if (GUILayout.Button("보스 처치")) boss.TakeDamage(DamageInfo.Simple(boss.CurrentHP, DamageSource.Player));
        }
        if (GUILayout.Button("영구 재화 +100") && GameServices.Save != null)
        {
            GameServices.Save.Data.PermanentCurrency += 100;
            GameServices.Save.TrySave();
        }
        if (player != null) DrawStats(player);
        if (GameServices.Save != null)
        {
            if (GUILayout.Button("저장 실패 흉내 켜기")) GameServices.Save.SetSimulateFailure(true);
            if (GUILayout.Button("저장 실패 흉내 끄기")) GameServices.Save.SetSimulateFailure(false);
        }
    }

    // 영구 성장, 아티팩트가 실제로 들어갔는지 확인용 최종값
    private static void DrawStats(PlayerController player)
    {
        PlayerStats stats = player.Stats;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("최종 스탯", EditorStyles.boldLabel);
        int used = MetaProgression.GrowthPointsUsed;
        EditorGUILayout.LabelField("영구 성장", stats.HasSource(StatSources.PermanentGrowth)
            ? $"등록됨 (배치 {used}P / 구매 {MetaProgression.GrowthPointsPurchased}P)"
            : used > 0 ? "배치는 있는데 등록 안 됨" : "배치 없음");
        EditorGUILayout.LabelField("최대 HP", $"{player.Health.CurrentHP} / {player.Health.MaxHP}");
        EditorGUILayout.LabelField("치명타 확률 / 피해", $"{stats.Get(StatId.CritChance) * 100f:0.#}% / x{stats.Get(StatId.CritDamage):0.##}");
        EditorGUILayout.LabelField("이동속도", stats.Get(StatId.MoveSpeed).ToString("0.##"));
        EditorGUILayout.LabelField("회피 충전 / 시간", $"{stats.GetInt(StatId.DodgeMaxCharge)} / {stats.Get(StatId.DodgeChargeTime):0.##}초");
        WeaponRuntime w = player.Weapons.CurrentWeapon;
        if (w != null)
            EditorGUILayout.LabelField(w.Definition.WeaponName, $"피해 {stats.GetWeaponDamageRaw(w.Type):0.##} · 탄창 {w.MaxAmmo} · 재장전 {w.ReloadTime:0.##}초 · 발사 간격 {w.FireInterval:0.###}초");
    }

    private void OnInspectorUpdate() => Repaint();
}
