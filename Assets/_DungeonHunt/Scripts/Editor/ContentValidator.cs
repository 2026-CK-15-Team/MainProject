using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 빌드 전 자동 검사 (03 §8.1, §8.5 / 04 §10 / 07 §7). 결과는 콘솔에. 오류가 있어도 빌드는 막지 않음 (기획 방침).
public static class ContentValidator
{
    [MenuItem("DungeonHunt/Validate/All Content", priority = 20)]
    public static void ValidateAll()
    {
        GameConfig config = FindConfig();
        if (config == null)
        {
            Debug.LogError("[검증] GameConfig 없음");
            return;
        }
        int errors = 0;
        errors += ValidateRooms(config);
        errors += ValidateMonsters(config);
        errors += ValidatePresets(config);
        errors += ValidateArtifacts(config);
        if (errors == 0) Debug.Log("[검증] 문제 없음");
        else Debug.LogWarning($"[검증] 문제 {errors}건. 위 로그 확인");
    }

    private static GameConfig FindConfig()
    {
        string guid = AssetDatabase.FindAssets("t:GameConfig").FirstOrDefault();
        return guid != null ? AssetDatabase.LoadAssetAtPath<GameConfig>(AssetDatabase.GUIDToAssetPath(guid)) : null;
    }

    // 03 §8.5 레이아웃 검수 중 지점 개수/위치 자동 항목
    private static int ValidateRooms(GameConfig config)
    {
        int errors = 0;
        if (config.Run == null || config.Run.Floors == null) { Debug.LogError("[검증] RunConfig 없음"); return 1; }
        var seen = new HashSet<Room>();
        foreach (FloorConfig floor in config.Run.Floors)
        {
            if (floor == null || floor.RoomPrefabs == null) continue;
            ValidateTileset(floor);
            foreach (Room room in floor.RoomPrefabs)
            {
                if (room == null || !seen.Add(room)) continue;
                RoomPoint[] points = room.GetComponentsInChildren<RoomPoint>(true);
                int teleport = points.Count(p => p.Kind == RoomPointKind.TeleportArrival);
                int reward = points.Count(p => p.Kind == RoomPointKind.RewardSpawn);
                int objects = points.Count(p => p.Kind == RoomPointKind.RoomObject);
                RoomType type = room.Template.Type;

                if (teleport != 1) errors += Error(room, $"TeleportArrivalPoint {teleport}개 (정확히 1개)");
                bool needsReward = type == RoomType.Combat || type == RoomType.Secret || (type == RoomType.Boss && points.Any(p => p.Kind == RoomPointKind.MonsterSpawn));
                if (needsReward && reward != 1) errors += Error(room, $"RewardSpawnPoint {reward}개 (정확히 1개)");
                int expectedObjects = type switch
                {
                    RoomType.Treasure => 1,
                    RoomType.Shop => 1,
                    RoomType.Gamble => 1,
                    RoomType.Secret => 1,
                    RoomType.Stairs => -1, // 출구(1) 또는 엔딩 룸(일반 보물상자 + 엔딩 상자)
                    _ => 0,
                };
                if (expectedObjects >= 0 && objects != expectedObjects) errors += Error(room, $"RoomObjectPoint {objects}개 (기대 {expectedObjects})");
                if (type == RoomType.Stairs)
                {
                    bool chests = points.Any(p => p.Role == RoomObjectRole.NormalChest) && points.Any(p => p.Role == RoomObjectRole.EndingChest);
                    if (!chests) Debug.LogWarning($"[검증] {room.name}: 마지막 층 엔딩 룸으로 쓰려면 NormalChest/EndingChest 역할 지점 필요", room);
                }
                var spawnIndices = points.Where(p => p.Kind == RoomPointKind.MonsterSpawn).Select(p => p.Index).ToList();
                if (spawnIndices.Count != spawnIndices.Distinct().Count()) errors += Error(room, "MonsterSpawn 번호 중복");
                MonsterWavePreset own = MonsterWavePreset.FromPoints(points.Where(p => p.Kind == RoomPointKind.MonsterSpawn), room.name);
                if (own != null)
                {
                    if (!own.IsCompatible(null, out string waveError)) errors += Error(room, $"스폰점 지정 몬스터: {waveError}");
                    if (type == RoomType.Boss && own.Waves.Count != 2) errors += Error(room, $"스폰점 지정 몬스터: EliteGate는 2웨이브 (지금 {own.Waves.Count}, 03-6 §7)");
                    if (floor == config.Run.Floors[0] && own.ContainsGuardian()) Debug.LogWarning($"[검증] {room.name}: 1층에서 쓰는 스폰점 지정 몬스터에 원래 수호자 포함", room);
                    Object.DestroyImmediate(own);
                }
                foreach (RoomPoint p in points.Where(p => p.Kind != RoomPointKind.MonsterSpawn && p.Spawns.Count > 0))
                    Debug.LogWarning($"[검증] {room.name}/{p.name}: MonsterSpawn이 아닌 지점의 Spawns는 무시됨", p);
                if (room.Template.Size == Vector2Int.zero) errors += Error(room, "Bake 안 됨 (크기 0)");
                foreach (RoomDoor door in room.GetComponentsInChildren<RoomDoor>(true))
                {
                    if (door.GetComponent<Collider2D>() == null) errors += Error(room, $"{door.name}: Collider2D 없음");
                }
            }
        }
        return errors;
    }

    // 코드 오토타일 타일셋: 빈 칸은 같은 분류 기본 칸으로 대체되므로 경고만. 기본 칸까지 비면 그 분류는 안 그려짐
    private static void ValidateTileset(FloorConfig floor)
    {
        DungeonTileset set = floor.Tileset;
        if (set == null)
        {
            Debug.LogWarning($"[검증] {floor.name}: Tileset 없음 → 오토타일 안 함. 방 프리팹에 칠한 벽/바닥 그림이 그대로 보임", floor);
            return;
        }
        var missing = new List<string>();
        if (set.FloorCenter == null || set.FloorCenter.Length == 0) missing.Add("바닥 F_C");
        if (set.FaceLow.Middle == null || set.FaceLow.Middle.Length == 0) missing.Add("벽 정면 W1 가운데");
        if (set.CeilingBottom == null && set.CeilingFill == null) missing.Add("천장 C_B (아래 벽 앞면)");
        if (set.CeilingFill == null) missing.Add("천장 채움 C_Fill (벽 너머, 없으면 G로 칠함)");
        if (set.CeilingFill == null && set.CeilingTop == null) missing.Add("천장 C_T 또는 C_Fill");
        if (set.Gap == null || set.Gap.Length == 0) missing.Add("틈 G (미방문 룸 가림)");
        if (missing.Count > 0) Debug.LogWarning($"[검증] {set.name}: 기본 칸 비어 있음 ({string.Join(", ", missing)})", set);
    }

    private static int ValidateMonsters(GameConfig config)
    {
        int errors = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:MonsterDefinition"))
        {
            var def = AssetDatabase.LoadAssetAtPath<MonsterDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (def != null && !def.Validate(out string error)) errors += Error(def, $"사용 불가 몬스터: {error}");
        }
        return errors;
    }

    // 03 §8.1 허용 목록 검증 (스폰점 번호는 층의 전투룸 레이아웃 기준)
    private static int ValidatePresets(GameConfig config)
    {
        int errors = 0;
        if (config.Floors == null) return 0;
        for (int f = 0; f < config.Floors.Length; f++)
        {
            FloorContent content = config.Floors[f];
            if (content == null) continue;
            FloorConfig floor = config.Run != null && f < config.Run.Floors.Length ? config.Run.Floors[f] : null;
            var combatRooms = floor != null && floor.RoomPrefabs != null ? floor.RoomPrefabs.Where(r => r != null && r.Template.Type == RoomType.Combat).ToList() : new List<Room>();

            foreach (MonsterWavePreset preset in content.CombatPresets)
            {
                if (preset == null) continue;
                int compatible = 0;
                foreach (Room room in combatRooms)
                {
                    var indices = new HashSet<int>(room.GetComponentsInChildren<RoomPoint>(true).Where(p => p.Kind == RoomPointKind.MonsterSpawn).Select(p => p.Index));
                    if (preset.IsCompatible(indices, out _)) compatible++;
                }
                if (combatRooms.Count > 0 && compatible == 0) errors += Error(preset, $"{f + 1}층 어떤 전투룸 레이아웃과도 맞지 않음 (빈 허용 목록)");
                if (f == 0 && preset.ContainsGuardian()) errors += Error(preset, "1층 구성에 원래 수호자(돌진형/저격형) 포함 → 허용 목록 제외");
            }
            if (f < config.FloorCount - 1)
            {
                if (content.ElitePresets.Count != 3) Debug.LogWarning($"[검증] {f + 1}층 EliteGate 프리셋 {content.ElitePresets.Count}종 (기획 3종, 사람 검수 대상)");
                foreach (MonsterWavePreset elite in content.ElitePresets)
                    if (elite != null && elite.Waves.Count != 2) errors += Error(elite, "EliteGate 프리셋은 2웨이브 (03-6 §7)");
            }
            else if (content.Boss == null)
            {
                Debug.LogWarning($"[검증] 마지막 층 BossDefinition 없음 (기본값 사용)");
            }
            else errors += ValidateBoss(content.Boss);
        }
        return errors;
    }

    // 05 보스 데이터 조건: 고정 순서 번호, 방향을 따라가는 패턴의 예고/회차 간격 >= 방향 잠금 시간 (04 §10)
    private static int ValidateBoss(BossDefinition boss)
    {
        int errors = 0;
        List<BossPatternData> patterns = boss.Patterns;
        if (patterns == null || patterns.Count == 0) { Debug.LogWarning($"[검증] {boss.name}: 패턴 목록 비어 있음 → 기본 5종 사용", boss); return 0; }
        float lockLead = boss.SniperReference != null ? boss.SniperReference.AimLockLeadTime : MonsterDefinition.CreateDefault(MonsterArchetype.Sniper).AimLockLeadTime;
        foreach (int n in boss.FixedOrder ?? new int[0])
            if (n < 1 || n > patterns.Count) errors += Error(boss, $"고정 순서의 패턴 번호 {n} 없음 (1~{patterns.Count})");
        if (!patterns.Any(p => !p.LowHPOnly)) errors += Error(boss, "HP 50% 이상에서 쓸 패턴이 없음 (전부 LowHPOnly)");
        for (int i = 0; i < patterns.Count; i++)
        {
            BossPatternData p = patterns[i];
            string id = $"패턴 {i + 1}({p.Kind})";
            bool tracks = p.Kind == BossPatternKind.AimedFan || p.Kind == BossPatternKind.Laser;
            if (tracks && p.TelegraphDuration < lockLead) errors += Error(boss, $"{id}: 예고 {p.TelegraphDuration}초 < 방향 잠금 {lockLead}초");
            if (tracks && p.Volleys > 1 && p.VolleyInterval < lockLead) errors += Error(boss, $"{id}: 회차 간격 {p.VolleyInterval}초 < 방향 잠금 {lockLead}초");
            if (p.Volleys > 1 && p.VolleyInterval <= 0f) errors += Error(boss, $"{id}: 회차 간격 0 이하");
            if (p.Kind != BossPatternKind.Laser && p.Speed <= 0f) errors += Error(boss, $"{id}: 탄속 0 이하");
            if (p.Kind == BossPatternKind.GapRing && p.GapSize >= p.Count) errors += Error(boss, $"{id}: 빈틈 {p.GapSize}발 >= 링 {p.Count}발");
            if (p.Kind == BossPatternKind.Splitter && p.SplitDistance <= 0f) errors += Error(boss, $"{id}: 분열 거리 0 이하");
        }
        return errors;
    }

    // 07 §7 카탈로그 산식과 해금 출처 몫
    private static int ValidateArtifacts(GameConfig config)
    {
        int errors = 0;
        ArtifactCatalog catalog = config.Artifacts;
        if (catalog == null) return Error(null, "ArtifactCatalog 없음");
        var ids = catalog.Artifacts.Where(a => a != null).Select(a => a.Id).ToList();
        if (ids.Count != ids.Distinct().Count()) errors += Error(catalog, "아티팩트 ID 중복");
        int setCount = catalog.Sets.Count(s => s != null);
        int universal = catalog.Artifacts.Count(a => a != null && a.Set == null);
        int total = ids.Count;
        if (total != setCount * 4 + universal) errors += Error(catalog, $"총수 {total} ≠ 세트 {setCount} x 4 + 범용 {universal}");
        foreach (ArtifactSetDefinition set in catalog.Sets.Where(s => s != null))
        {
            int count = catalog.Artifacts.Count(a => a != null && a.Set == set);
            if (count != 4) errors += Error(set, $"세트 아티팩트 {count}개 (4개)");
        }
        int initial = catalog.Artifacts.Count(a => a != null && a.UnlockSource == ArtifactUnlockSource.Initial);
        int currency = catalog.Artifacts.Count(a => a != null && a.UnlockSource == ArtifactUnlockSource.Currency);
        int progress = catalog.Artifacts.Count(a => a != null && a.UnlockSource == ArtifactUnlockSource.Progress);
        // 해금 출처 몫은 콘텐츠 표가 정함 (현재 15/9/4). 기획서 06 §7 기준값과 다르면 경고만
        (int i, int c, int p) expected = total == 28 ? (14, 10, 4) : total == 20 ? (10, 6, 4) : (-1, -1, -1);
        if (expected.i >= 0 && (initial != expected.i || currency != expected.c || progress != expected.p))
            Debug.LogWarning($"[검증] 해금 출처 몫 {initial}/{currency}/{progress} (기획서 06 §7 기준 {expected.i}/{expected.c}/{expected.p})", catalog);
        int linked = new[] { catalog.ReachFloor2, catalog.ReachFloor3, catalog.FirstClear, catalog.SecondClear }.Count(a => a != null && a.UnlockSource == ArtifactUnlockSource.Progress);
        if (linked != progress) errors += Error(catalog, $"진행 해금 아티팩트 {progress}개 중 조건에 연결된 것 {linked}개");
        int testCount = catalog.Artifacts.Count(a => a != null && a.IsTestPlaceholder);
        if (testCount > 0) Debug.LogWarning($"[검증] 테스트 더미 아티팩트 {testCount}개 포함 (기획 확정 콘텐츠로 교체 필요)");
        // EliteGate 보상 후보: 초기 해금 중 희귀 이상이 있어야 첫 런부터 생성 가능
        if (!catalog.Artifacts.Any(a => a != null && a.UnlockSource == ArtifactUnlockSource.Initial && a.Grade != ArtifactGrade.Common))
            errors += Error(catalog, "초기 해금에 희귀 이상이 없음 → EliteGate 보상 생성 불가");
        return errors;
    }

    private static int Error(Object context, string message)
    {
        Debug.LogError($"[검증] {(context != null ? context.name + ": " : "")}{message}", context);
        return 1;
    }

    // 선택한 방 프리팹에 기본 지점 추가 (방 종류에 맞게)
    [MenuItem("DungeonHunt/Room/Add Missing Points To Selected Room", priority = 40)]
    public static void AddMissingPoints()
    {
        foreach (GameObject go in Selection.gameObjects)
        {
            Room room = go.GetComponent<Room>();
            if (room == null) continue;
            Undo.RegisterFullObjectHierarchyUndo(go, "Add Room Points");
            Vector2 center = (Vector2)room.Template.Size * 0.5f;
            RoomPoint[] points = go.GetComponentsInChildren<RoomPoint>(true);
            if (!points.Any(p => p.Kind == RoomPointKind.TeleportArrival)) Add(go.transform, RoomPointKind.TeleportArrival, center);
            RoomType type = room.Template.Type;
            if ((type == RoomType.Combat || type == RoomType.Secret) && !points.Any(p => p.Kind == RoomPointKind.RewardSpawn))
                Add(go.transform, RoomPointKind.RewardSpawn, center + Vector2.up);
            if ((type == RoomType.Treasure || type == RoomType.Shop || type == RoomType.Gamble || type == RoomType.Secret || type == RoomType.Stairs) && !points.Any(p => p.Kind == RoomPointKind.RoomObject))
                Add(go.transform, RoomPointKind.RoomObject, center + Vector2.up * 0.5f);
            EditorUtility.SetDirty(go);
        }
    }

    private static void Add(Transform parent, RoomPointKind kind, Vector2 position)
    {
        var go = new GameObject($"Point_{kind}");
        Undo.RegisterCreatedObjectUndo(go, "Add Room Point");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.AddComponent<RoomPoint>().Kind = kind;
    }

    [MenuItem("DungeonHunt/Save/Delete Save File", priority = 60)]
    public static void DeleteSave()
    {
        string path = System.IO.Path.Combine(Application.persistentDataPath, "permanent.json");
        if (System.IO.File.Exists(path) && EditorUtility.DisplayDialog("세이브 삭제", path + " 를 삭제할까요?", "삭제", "취소"))
        {
            System.IO.File.Delete(path);
            Debug.Log("세이브 삭제됨");
        }
    }

    [MenuItem("DungeonHunt/Save/Open Save Folder", priority = 61)]
    public static void OpenSaveFolder() => EditorUtility.RevealInFinder(Application.persistentDataPath);
}
