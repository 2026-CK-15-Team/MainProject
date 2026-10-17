using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

// 메뉴: DungeonHunt/Setup/Generate Placeholder Content
// 아트/콘텐츠가 없어도 바로 플레이해 볼 수 있게 자리 표시용 에셋을 한 번에 만듦.
// 만든 것은 전부 Assets/_DungeonHunt/Generated/ 아래 (Test_ 접두사). 기획 확정 콘텐츠가 아님.
// - 단색 타일 5종, 방 프리팹 (시작/전투 3종/보물/상점/도박/비밀/엘리트 1·2층/보스/출구), Bake까지
// - FloorConfig 3개, RunConfig, GameBalance, 몬스터 5종, 구성 프리셋, 보스, 테스트 아티팩트 28개, GameConfig
// - Game 씬 (GameBootstrap) + 빌드 설정 등록
public static class PlaceholderContentGenerator
{
    private const string Root = "Assets/_DungeonHunt/Generated";
    private const int DoorWidth = 2;
    private static readonly WallStyle Style = new WallStyle();

    private class TileSet
    {
        public Tile Floor, TopWall, BottomWall, BottomFront, SideWall;
    }

    [MenuItem("DungeonHunt/Setup/Generate Placeholder Content", priority = 1)]
    public static void GenerateAll()
    {
        if (!EditorUtility.DisplayDialog("자리 표시용 콘텐츠 생성",
                $"{Root} 아래에 테스트용 타일, 방 프리팹, 설정, 몬스터, 아티팩트, Game 씬을 만듭니다.\n같은 이름 에셋은 덮어씁니다.", "생성", "취소"))
            return;

        EnsureFolder(Root);
        foreach (string sub in new[] { "Tiles", "Rooms", "Config", "Monsters", "Presets", "Artifacts", "Weapons" })
            EnsureFolder($"{Root}/{sub}");
        EnsureFolder("Assets/_DungeonHunt/Resources");
        EnsureFolder("Assets/_DungeonHunt/Scenes");

        TileSet tiles = CreateTiles();
        Dictionary<string, Room> rooms = CreateRooms(tiles);
        RunConfig run = CreateRunConfig(tiles, rooms);
        GameBalance balance = CreateOrLoad<GameBalance>($"{Root}/Config/GameBalance.asset");
        Dictionary<MonsterArchetype, MonsterDefinition> monsters = CreateMonsters();
        FloorContent[] floors = CreateFloorContents(monsters);
        ArtifactCatalog catalog = CreateArtifacts();
        WeaponScriptable[] weapons = CreateWeapons();

        GameConfig config = CreateOrLoad<GameConfig>("Assets/_DungeonHunt/Resources/GameConfig.asset");
        config.Run = run;
        config.Balance = balance;
        config.Floors = floors;
        // 실제 콘텐츠(Content/Artifacts)가 있으면 그걸 유지, 없을 때만 테스트 카탈로그
        ArtifactCatalog content = AssetDatabase.LoadAssetAtPath<ArtifactCatalog>(ArtifactContentBuilder.CatalogPath);
        config.Artifacts = content != null ? content : catalog;
        config.Weapons = weapons;
        if (config.PostProcessProfile == null) config.PostProcessProfile = PostProcessSetup.Setup();
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();

        CreateScene(config);
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("완료", "생성 완료. Assets/_DungeonHunt/Scenes/Game.unity 를 열고 Play 하세요.", "확인");
    }

    // ---------------- 타일 ----------------

    private static TileSet CreateTiles()
    {
        return new TileSet
        {
            Floor = CreateTile("Test_Floor", new Color(0.32f, 0.29f, 0.27f), new Color(0.36f, 0.33f, 0.3f), Tile.ColliderType.None),
            TopWall = CreateTile("Test_TopWall", new Color(0.55f, 0.47f, 0.4f), new Color(0.47f, 0.4f, 0.34f), Tile.ColliderType.Grid),
            BottomWall = CreateTile("Test_BottomWall", new Color(0.2f, 0.17f, 0.15f), new Color(0.24f, 0.2f, 0.18f), Tile.ColliderType.Grid),
            BottomFront = CreateTile("Test_BottomWallFront", new Color(0.42f, 0.36f, 0.3f, 0.92f), new Color(0.38f, 0.32f, 0.27f, 0.92f), Tile.ColliderType.None),
            SideWall = CreateTile("Test_SideWall", new Color(0.45f, 0.39f, 0.33f), new Color(0.4f, 0.34f, 0.29f), Tile.ColliderType.Grid),
        };
    }

    private static Tile CreateTile(string name, Color main, Color edge, Tile.ColliderType collider)
    {
        const int size = 16;
        string texturePath = $"{Root}/Tiles/{name}.png";
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool border = x == 0 || y == 0 || x == size - 1 || y == size - 1;
                texture.SetPixel(x, y, border ? edge : main);
            }
        texture.Apply();
        File.WriteAllBytes(texturePath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(texturePath);
        var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = size;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);

        Tile tile = CreateOrLoad<Tile>($"{Root}/Tiles/{name}.asset");
        tile.sprite = sprite;
        tile.colliderType = collider;
        tile.color = Color.white;
        EditorUtility.SetDirty(tile);
        return tile;
    }

    // ---------------- 방 프리팹 ----------------

    private static Dictionary<string, Room> CreateRooms(TileSet tiles)
    {
        var rooms = new Dictionary<string, Room>
        {
            ["Start"] = CreateRoom("Test_Room_Start", RoomType.Start, 8, 6, tiles, RoomKind.Plain),
            ["Combat_A"] = CreateRoom("Test_Room_Combat_A", RoomType.Combat, 12, 8, tiles, RoomKind.Combat),
            ["Combat_B"] = CreateRoom("Test_Room_Combat_B", RoomType.Combat, 12, 8, tiles, RoomKind.CombatPillars2),
            ["Combat_C"] = CreateRoom("Test_Room_Combat_C", RoomType.Combat, 12, 8, tiles, RoomKind.CombatPillars4),
            ["Treasure"] = CreateRoom("Test_Room_Treasure", RoomType.Treasure, 8, 6, tiles, RoomKind.Plain),
            ["Shop"] = CreateRoom("Test_Room_Shop", RoomType.Shop, 8, 6, tiles, RoomKind.Plain),
            ["Gamble"] = CreateRoom("Test_Room_Gamble", RoomType.Gamble, 8, 6, tiles, RoomKind.Plain),
            ["Secret"] = CreateRoom("Test_Room_Secret", RoomType.Secret, 8, 6, tiles, RoomKind.Secret),
            ["Elite1"] = CreateRoom("Test_Room_EliteGate_F1", RoomType.Boss, 14, 10, tiles, RoomKind.Combat),
            ["Elite2"] = CreateRoom("Test_Room_EliteGate_F2", RoomType.Boss, 16, 12, tiles, RoomKind.Combat),
            ["Boss"] = CreateRoom("Test_Room_FinalBoss", RoomType.Boss, 18, 12, tiles, RoomKind.Boss),
            ["Stairs"] = CreateRoom("Test_Room_Exit", RoomType.Stairs, 8, 6, tiles, RoomKind.Exit),
        };
        return rooms;
    }

    private enum RoomKind { Plain, Combat, CombatPillars2, CombatPillars4, Secret, Boss, Exit }

    // 바운딩 = 바닥(w x h) + 벽 (위 3 / 아래 2 / 좌우 1). 왼쪽 아래 (0,0). 네 변 가운데에 폭 2 문.
    private static Room CreateRoom(string name, RoomType type, int w, int h, TileSet tiles, RoomKind kind)
    {
        int S = Style.Side, T = Style.Top, B = Style.Bottom;
        int W = w + S * 2, H = h + T + B;

        var root = new GameObject(name);
        root.AddComponent<Grid>().cellSize = new Vector3(1f, 1f, 0f);
        Tilemap floor = GameBootstrap.CreateTilemap(root.transform, "Floor", GameBootstrap.FloorSortingOrder, collider: false);
        Tilemap wall = GameBootstrap.CreateTilemap(root.transform, "Wall", GameBootstrap.WallSortingOrder, collider: true);
        Tilemap front = GameBootstrap.CreateTilemap(root.transform, "WallFront", GameBootstrap.WallFrontSortingOrder, collider: false);

        for (int x = 0; x < W; x++)
        {
            for (int y = 0; y < H; y++)
            {
                var cell = new Vector3Int(x, y, 0);
                bool side = x < S || x >= W - S;
                bool top = y >= H - T;
                bool bottomOuter = y == 0;
                bool bottomFront = !side && y >= 1 && y < B;
                if (side) wall.SetTile(cell, tiles.SideWall);
                else if (top) wall.SetTile(cell, tiles.TopWall);
                else if (bottomOuter) wall.SetTile(cell, tiles.BottomWall);
                else floor.SetTile(cell, tiles.Floor);
                if (bottomFront) front.SetTile(cell, tiles.BottomFront);
            }
        }

        // 문 (4면, 가운데)
        int doorX = S + (w - DoorWidth) / 2;
        int doorY = B + (h - DoorWidth) / 2;
        for (int i = 0; i < DoorWidth; i++)
        {
            for (int y = H - T; y < H; y++) Carve(floor, wall, front, new Vector3Int(doorX + i, y, 0), tiles);
            for (int y = 0; y < B; y++) Carve(floor, wall, front, new Vector3Int(doorX + i, y, 0), tiles);
            for (int x = 0; x < S; x++) Carve(floor, wall, front, new Vector3Int(x, doorY + i, 0), tiles);
            for (int x = W - S; x < W; x++) Carve(floor, wall, front, new Vector3Int(x, doorY + i, 0), tiles);
        }
        CreateDoor(root.transform, RoomSide.Up, new Vector2(doorX + DoorWidth * 0.5f, H - T * 0.5f), new Vector2(DoorWidth, T));
        // 아래 문 장벽은 충돌하는 바깥 줄만. 앞면 줄까지 덮으면 잠글 때 옆 벽보다 한 칸 튀어나옴
        int solidB = B - Style.FrontRowCount;
        CreateDoor(root.transform, RoomSide.Down, new Vector2(doorX + DoorWidth * 0.5f, solidB * 0.5f), new Vector2(DoorWidth, solidB));
        CreateDoor(root.transform, RoomSide.Left, new Vector2(S * 0.5f, doorY + DoorWidth * 0.5f), new Vector2(S, DoorWidth));
        CreateDoor(root.transform, RoomSide.Right, new Vector2(W - S * 0.5f, doorY + DoorWidth * 0.5f), new Vector2(S, DoorWidth));

        // 장애물 (벽 타일로 기둥)
        var interior = new RectInt(S, B, w, h);
        Vector2 center = new Vector2(S + w * 0.5f, B + h * 0.5f);
        var pillars = new List<Vector2Int>();
        if (kind == RoomKind.CombatPillars2)
        {
            pillars.Add(new Vector2Int(S + 3, B + h / 2 - 1));
            pillars.Add(new Vector2Int(S + w - 5, B + h / 2 - 1));
        }
        else if (kind == RoomKind.CombatPillars4)
        {
            pillars.Add(new Vector2Int(S + 3, B + 2));
            pillars.Add(new Vector2Int(S + w - 5, B + 2));
            pillars.Add(new Vector2Int(S + 3, B + h - 4));
            pillars.Add(new Vector2Int(S + w - 5, B + h - 4));
        }
        foreach (Vector2Int p in pillars)
            for (int dx = 0; dx < 2; dx++)
                for (int dy = 0; dy < 2; dy++)
                {
                    var cell = new Vector3Int(p.x + dx, p.y + dy, 0);
                    floor.SetTile(cell, null);
                    wall.SetTile(cell, tiles.SideWall);
                }

        // 고정 지점
        AddPoint(root.transform, RoomPointKind.TeleportArrival, center + new Vector2(0f, -1f));
        switch (type)
        {
            case RoomType.Combat:
            case RoomType.Boss:
                if (kind != RoomKind.Boss) AddPoint(root.transform, RoomPointKind.RewardSpawn, center + new Vector2(0f, 1f));
                if (kind != RoomKind.Boss) AddSpawnPoints(root.transform, interior, pillars, doorX, doorY, 10);
                break;
            case RoomType.Treasure:
            case RoomType.Shop:
            case RoomType.Gamble:
                AddPoint(root.transform, RoomPointKind.RoomObject, center + new Vector2(0f, 0.5f));
                break;
            case RoomType.Secret:
                AddPoint(root.transform, RoomPointKind.RewardSpawn, center + new Vector2(-1.5f, 0.5f));
                AddPoint(root.transform, RoomPointKind.RoomObject, center + new Vector2(1.5f, 0.5f));
                break;
            case RoomType.Stairs:
                // 1, 2층 FloorExitRoom: Default / 3층 RunEndTreasureRoom: 일반 보물상자 + 엔딩 상자
                AddPoint(root.transform, RoomPointKind.RoomObject, center + new Vector2(0f, 0.5f));
                AddPoint(root.transform, RoomPointKind.RoomObject, center + new Vector2(-2f, 0.5f), RoomObjectRole.NormalChest);
                AddPoint(root.transform, RoomPointKind.RoomObject, center + new Vector2(2f, 0.5f), RoomObjectRole.EndingChest);
                break;
        }

        Room room = root.AddComponent<Room>();
        var so = new SerializedObject(room);
        so.FindProperty("template.type").enumValueIndex = (int)type;
        so.FindProperty("floorTilemap").objectReferenceValue = floor;
        so.FindProperty("wallTilemap").objectReferenceValue = wall;
        so.FindProperty("wallFrontTilemap").objectReferenceValue = front;
        so.ApplyModifiedPropertiesWithoutUndo();
        RoomBaker.Bake(room);

        string path = $"{Root}/Rooms/{name}.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab.GetComponent<Room>();
    }

    private static void Carve(Tilemap floor, Tilemap wall, Tilemap front, Vector3Int cell, TileSet tiles)
    {
        wall.SetTile(cell, null);
        front.SetTile(cell, null);
        floor.SetTile(cell, tiles.Floor);
    }

    private static void CreateDoor(Transform parent, RoomSide side, Vector2 center, Vector2 size)
    {
        var go = new GameObject($"Door_{side}");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = center;
        var box = go.AddComponent<BoxCollider2D>();
        box.size = size;
        int wallLayer = GameLayers.Wall;
        if (wallLayer >= 0) go.layer = wallLayer;
        try { go.tag = GameLayers.WallTag; } catch (UnityException) { }
        RoomDoor door = go.AddComponent<RoomDoor>();
        var so = new SerializedObject(door);
        so.FindProperty("side").enumValueIndex = (int)side;
        so.FindProperty("barrier").objectReferenceValue = box;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AddPoint(Transform parent, RoomPointKind kind, Vector2 position, RoomObjectRole role = RoomObjectRole.Default, int index = 1)
    {
        var go = new GameObject(kind == RoomPointKind.MonsterSpawn ? $"Spawn_{index}" : role != RoomObjectRole.Default ? $"Point_{role}" : $"Point_{kind}");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        RoomPoint point = go.AddComponent<RoomPoint>();
        point.Kind = kind;
        point.Role = role;
        point.Index = index;
    }

    // 문 구역 앞 띠(깊이 2칸)와 양옆, 중앙, 기둥을 피해 고르게 스폰점 배치
    private static void AddSpawnPoints(Transform parent, RectInt interior, List<Vector2Int> pillars, int doorX, int doorY, int count)
    {
        var candidates = new List<Vector2>();
        for (int x = interior.xMin + 1; x < interior.xMax - 1; x++)
        {
            for (int y = interior.yMin + 1; y < interior.yMax - 1; y++)
            {
                bool nearHorizontalDoor = x >= doorX - 2 && x <= doorX + DoorWidth + 1 && (y < interior.yMin + 3 || y >= interior.yMax - 3);
                bool nearVerticalDoor = y >= doorY - 2 && y <= doorY + DoorWidth + 1 && (x < interior.xMin + 3 || x >= interior.xMax - 3);
                if (nearHorizontalDoor || nearVerticalDoor) continue;
                bool onPillar = pillars.Exists(p => x >= p.x - 1 && x <= p.x + 2 && y >= p.y - 1 && y <= p.y + 2);
                if (onPillar) continue;
                Vector2 c = interior.center;
                if (Mathf.Abs(x + 0.5f - c.x) < 2f && Mathf.Abs(y + 0.5f - c.y) < 2f) continue;
                candidates.Add(new Vector2(x + 0.5f, y + 0.5f));
            }
        }
        // 서로 먼 지점부터 고름
        var chosen = new List<Vector2>();
        if (candidates.Count > 0) chosen.Add(candidates[0]);
        while (chosen.Count < count && chosen.Count < candidates.Count)
        {
            Vector2 best = candidates[0];
            float bestDistance = -1f;
            foreach (Vector2 c in candidates)
            {
                float d = float.MaxValue;
                foreach (Vector2 picked in chosen) d = Mathf.Min(d, Vector2.Distance(c, picked));
                if (d > bestDistance) { bestDistance = d; best = c; }
            }
            chosen.Add(best);
        }
        for (int i = 0; i < chosen.Count; i++)
            AddPoint(parent, RoomPointKind.MonsterSpawn, chosen[i], RoomObjectRole.Default, i + 1);
    }

    // ---------------- 던전 설정 ----------------

    private static RunConfig CreateRunConfig(TileSet tiles, Dictionary<string, Room> rooms)
    {
        int[] combatCounts = { 4, 6, 8 };
        bool[] loops = { false, true, true };
        string[] bossKeys = { "Elite1", "Elite2", "Boss" };
        var floors = new FloorConfig[3];
        for (int f = 0; f < 3; f++)
        {
            FloorConfig floor = CreateOrLoad<FloorConfig>($"{Root}/Config/Test_Floor{f + 1}.asset");
            floor.CombatRoomCount = combatCounts[f];
            floor.HasLoop = loops[f];
            floor.HasStairs = true; // 3층의 계단방 = RunEndTreasureRoom
            floor.MinRoomSpacing = 3;
            floor.RoomPrefabs = new[]
            {
                rooms["Start"], rooms["Combat_A"], rooms["Combat_B"], rooms["Combat_C"], rooms["Treasure"], rooms["Shop"],
                rooms["Gamble"], rooms["Secret"], rooms[bossKeys[f]], rooms["Stairs"],
            };
            floor.CorridorFloorTile = tiles.Floor;
            floor.TopWallTile = tiles.TopWall;
            floor.BottomWallTile = tiles.BottomWall;
            floor.BottomWallFrontTile = tiles.BottomFront;
            floor.SideWallTile = tiles.SideWall;
            EditorUtility.SetDirty(floor);
            floors[f] = floor;
        }
        RunConfig run = CreateOrLoad<RunConfig>($"{Root}/Config/Test_RunConfig.asset");
        run.Floors = floors;
        run.DoubleGambleChance = 0.35f;
        run.DoubleSecretChance = 0.3f;
        EditorUtility.SetDirty(run);
        return run;
    }

    // ---------------- 몬스터와 프리셋 ----------------

    private static Dictionary<MonsterArchetype, MonsterDefinition> CreateMonsters()
    {
        var result = new Dictionary<MonsterArchetype, MonsterDefinition>();
        foreach (MonsterArchetype archetype in System.Enum.GetValues(typeof(MonsterArchetype)))
        {
            MonsterDefinition def = CreateOrLoad<MonsterDefinition>($"{Root}/Monsters/Test_{archetype}.asset");
            def.Archetype = archetype;
            def.ResetToArchetypeDefaults();
            EditorUtility.SetDirty(def);
            result[archetype] = def;
        }
        return result;
    }

    private static FloorContent[] CreateFloorContents(Dictionary<MonsterArchetype, MonsterDefinition> m)
    {
        MonsterDefinition br = m[MonsterArchetype.Brawler], sh = m[MonsterArchetype.Shooter], ba = m[MonsterArchetype.Barrage];
        MonsterDefinition ch = m[MonsterArchetype.Charger], sn = m[MonsterArchetype.Sniper];

        // 1층: 외부 침입자만 (원래 수호자 없음, 04 §4.4)
        var f1Combat = new[]
        {
            new[] { br, br, sh, sh }, new[] { br, sh, ba, br }, new[] { sh, sh, ba, br }, new[] { br, br, br, ba },
        };
        var f2Combat = new[]
        {
            new[] { br, br, sh, ba, ch }, new[] { br, sh, sh, ba, sn }, new[] { ch, br, sh, ba, br }, new[] { br, ba, ba, sh, ch },
            new[] { sh, sh, br, br, sn }, new[] { ch, ch, br, sh, ba },
        };
        var f3Combat = new[]
        {
            new[] { br, br, sh, ba, ch, sn }, new[] { ch, ch, br, sh, sh, ba }, new[] { sn, sn, br, br, sh, ba }, new[] { ch, br, br, ba, ba, sh },
            new[] { sh, sh, sh, br, ch, sn }, new[] { ba, ba, br, ch, ch, sn }, new[] { br, br, br, sh, sn, ch }, new[] { ch, sh, ba, sn, br, br },
        };
        // EliteGate: 1층 5+6, 2층 6+8 (03-6 §7). 2층은 원거리 역할이 1층 최댓값 + 1 이상 (03-6 §3)
        var f1Elite = new[]
        {
            new[] { new[] { br, br, sh, sh, br }, new[] { br, br, sh, sh, br, br } },
            new[] { new[] { br, sh, br, sh, br }, new[] { br, br, br, sh, sh, sh } },
            new[] { new[] { br, br, br, sh, sh }, new[] { br, sh, sh, br, br, br } },
        };
        var f2Elite = new[]
        {
            new[] { new[] { br, br, sh, ba, ch, br }, new[] { br, br, sh, sh, ba, ba, ch, ch } },
            new[] { new[] { br, sh, ba, br, sn, ch }, new[] { br, br, sh, ba, sn, ch, ch, br } },
            new[] { new[] { ch, br, sh, sh, ba, br }, new[] { br, ch, sh, ba, ba, sn, br, br } },
        };

        var contents = new FloorContent[3];
        var combat = new[] { f1Combat, f2Combat, f3Combat };
        for (int f = 0; f < 3; f++)
        {
            FloorContent content = CreateOrLoad<FloorContent>($"{Root}/Config/Test_FloorContent{f + 1}.asset");
            content.CombatPresets = new List<MonsterWavePreset>();
            for (int i = 0; i < combat[f].Length; i++)
                content.CombatPresets.Add(CreatePreset($"Test_F{f + 1}_Combat{i + 1}", new[] { combat[f][i] }));
            content.ElitePresets = new List<MonsterWavePreset>();
            if (f == 0) for (int i = 0; i < 3; i++) content.ElitePresets.Add(CreatePreset($"Test_F1_Elite{i + 1}", f1Elite[i]));
            if (f == 1) for (int i = 0; i < 3; i++) content.ElitePresets.Add(CreatePreset($"Test_F2_Elite{i + 1}", f2Elite[i]));
            content.FallbackMonsters = f == 0 ? new List<MonsterDefinition> { br, sh, ba } : new List<MonsterDefinition> { br, sh, ba, ch, sn };
            content.FallbackMonsterCount = 4 + f;
            if (f == 2)
            {
                BossDefinition boss = CreateOrLoad<BossDefinition>($"{Root}/Monsters/Test_FinalBoss.asset");
                boss.UsePlaceholderPatterns = true; // 패턴 미정 → 테스트용 더미
                EditorUtility.SetDirty(boss);
                content.Boss = boss;
            }
            EditorUtility.SetDirty(content);
            contents[f] = content;
        }
        return contents;
    }

    private static MonsterWavePreset CreatePreset(string name, MonsterDefinition[][] waves)
    {
        MonsterWavePreset preset = CreateOrLoad<MonsterWavePreset>($"{Root}/Presets/{name}.asset");
        preset.Waves = new List<MonsterWavePreset.Wave>();
        foreach (MonsterDefinition[] wave in waves)
        {
            var w = new MonsterWavePreset.Wave();
            for (int i = 0; i < wave.Length; i++)
                w.Entries.Add(new MonsterWavePreset.SpawnEntry { SpawnPoint = i + 1, Monster = wave[i], CurrencyDrop = 1 });
            preset.Waves.Add(w);
        }
        EditorUtility.SetDirty(preset);
        return preset;
    }

    // ---------------- 아티팩트 (테스트 더미) ----------------

    private static ArtifactCatalog CreateArtifacts()
    {
        var setInfos = new (string id, string name, Color color, bool spec, WeaponType weapon, StatModifier[] mods)[]
        {
            ("Pistol", "권총", new Color(0.4f, 0.6f, 0.95f), true, WeaponType.Pistol, new[] { M(StatId.MoveSpeed, ModifierKind.Percent, 0.05f), M(StatId.DodgeChargeTime, ModifierKind.Percent, -0.06f), M(StatId.PistolDamage, ModifierKind.Percent, 0.1f), M(StatId.MoveSpeed, ModifierKind.Percent, 0.1f) }),
            ("Dodge", "회피 강화", new Color(0.5f, 0.9f, 1f), false, WeaponType.Pistol, new[] { M(StatId.DodgeChargeTime, ModifierKind.Percent, -0.08f), M(StatId.DodgeChargeTime, ModifierKind.Percent, -0.1f), M(StatId.DodgeChargeTime, ModifierKind.Percent, -0.06f), M(StatId.DodgeMaxCharge, ModifierKind.Flat, 1f) }),
            ("Rifle", "라이플", new Color(0.5f, 0.85f, 0.5f), true, WeaponType.Rifle, new[] { M(StatId.RifleDamage, ModifierKind.Percent, 0.1f), M(StatId.RifleRange, ModifierKind.Flat, 1f), M(StatId.RifleReloadTime, ModifierKind.Percent, -0.1f), M(StatId.RifleDamage, ModifierKind.Percent, 0.2f) }),
            ("Shotgun", "샷건", new Color(0.95f, 0.55f, 0.3f), true, WeaponType.Shotgun, new[] { M(StatId.ShotgunDamage, ModifierKind.Percent, 0.1f), M(StatId.ShotgunMagazine, ModifierKind.Flat, 1f), M(StatId.ShotgunReloadTime, ModifierKind.Percent, -0.1f), M(StatId.ShotgunDamage, ModifierKind.Percent, 0.2f) }),
            ("Swap", "무기 스왑 유도", new Color(0.85f, 0.8f, 0.35f), false, WeaponType.Pistol, new[] { M(StatId.SwapReadyDelay, ModifierKind.Flat, -0.03f), M(StatId.ReloadTimeAll, ModifierKind.Percent, -0.08f), M(StatId.ReloadTimeAll, ModifierKind.Percent, -0.05f), M(StatId.ReloadTimeAll, ModifierKind.Percent, -0.12f) }),
            ("AllIn", "전투력 올인", new Color(0.9f, 0.3f, 0.35f), false, WeaponType.Pistol, new[] { M(StatId.WeaponDamageAll, ModifierKind.Percent, 0.12f), M(StatId.WeaponDamageAll, ModifierKind.Percent, 0.15f), M(StatId.WeaponDamageAll, ModifierKind.Percent, 0.1f), M(StatId.WeaponDamageAll, ModifierKind.Percent, 0.25f) }),
        };
        ArtifactGrade[] setGrades = { ArtifactGrade.Common, ArtifactGrade.Rare, ArtifactGrade.Common, ArtifactGrade.Legendary };

        ArtifactCatalog catalog = CreateOrLoad<ArtifactCatalog>($"{Root}/Artifacts/Test_ArtifactCatalog.asset");
        catalog.Sets = new List<ArtifactSetDefinition>();
        catalog.Artifacts = new List<ArtifactDefinition>();

        for (int s = 0; s < setInfos.Length; s++)
        {
            var info = setInfos[s];
            ArtifactSetDefinition set = CreateOrLoad<ArtifactSetDefinition>($"{Root}/Artifacts/Test_Set_{info.id}.asset");
            set.Id = "Test_Set_" + info.id;
            set.DisplayName = info.name;
            set.Color = info.color;
            set.IsWeaponSpecialization = info.spec;
            set.SpecializedWeapon = info.weapon;
            set.CodexGroupOrder = s;
            set.Tier1Modifiers = new List<StatModifier>(); // Tier 효과 미정 (07 미정)
            set.Tier2Modifiers = new List<StatModifier>();
            EditorUtility.SetDirty(set);
            catalog.Sets.Add(set);

            for (int i = 0; i < 4; i++)
            {
                // 해금 출처 배정(14/10/4)은 기획 미정 → 테스트용 배정
                ArtifactUnlockSource source = i < 2 ? ArtifactUnlockSource.Initial : i == 2 ? ArtifactUnlockSource.Currency : (s < 4 ? ArtifactUnlockSource.Progress : ArtifactUnlockSource.Currency);
                ArtifactDefinition a = CreateArtifact($"Test_{info.id}_{i + 1}", $"{info.name} 더미 {i + 1}", setGrades[i], set, source, info.mods[i], i);
                catalog.Artifacts.Add(a);
            }
        }

        var universal = new[]
        {
            (ArtifactGrade.Common, M(StatId.MaxHP, ModifierKind.Flat, 1f)),
            (ArtifactGrade.Rare, M(StatId.CritChance, ModifierKind.Flat, 0.05f)),
            (ArtifactGrade.Legendary, M(StatId.CritDamage, ModifierKind.Flat, 0.3f)),
            (ArtifactGrade.Common, M(StatId.MoveSpeed, ModifierKind.Percent, 0.06f)),
        };
        for (int i = 0; i < universal.Length; i++)
        {
            ArtifactUnlockSource source = i < 2 ? ArtifactUnlockSource.Initial : ArtifactUnlockSource.Currency;
            catalog.Artifacts.Add(CreateArtifact($"Test_Universal_{i + 1}", $"범용 더미 {i + 1}", universal[i].Item1, null, source, universal[i].Item2, i));
        }

        catalog.ReachFloor2 = catalog.Find("Test_Pistol_4");
        catalog.ReachFloor3 = catalog.Find("Test_Dodge_4");
        catalog.FirstClear = catalog.Find("Test_Rifle_4");
        catalog.SecondClear = catalog.Find("Test_Shotgun_4");
        EditorUtility.SetDirty(catalog);
        return catalog;
    }

    private static StatModifier M(StatId stat, ModifierKind kind, float value) => new StatModifier(stat, kind, value);

    private static ArtifactDefinition CreateArtifact(string id, string displayName, ArtifactGrade grade, ArtifactSetDefinition set, ArtifactUnlockSource source, StatModifier mod, int order)
    {
        ArtifactDefinition a = CreateOrLoad<ArtifactDefinition>($"{Root}/Artifacts/{id}.asset");
        a.Id = id;
        a.DisplayName = displayName;
        a.Grade = grade;
        a.Set = set;
        a.UnlockSource = source;
        a.Modifiers = new List<StatModifier> { mod };
        a.Description = "테스트용 더미 효과";
        a.CodexOrder = order;
        a.IsTestPlaceholder = true;
        EditorUtility.SetDirty(a);
        return a;
    }

    // ---------------- 무기 ----------------

    private static WeaponScriptable[] CreateWeapons()
    {
        var result = new List<WeaponScriptable>();
        foreach (WeaponType type in new[] { WeaponType.Rifle, WeaponType.Pistol, WeaponType.Shotgun })
        {
            WeaponScriptable def = CreateOrLoad<WeaponScriptable>($"{Root}/Weapons/Test_{type}.asset");
            def.WeaponType = type;
            def.SwapReadyDelay = 0.12f;
            WeaponDefaults.Apply(def);
            EditorUtility.SetDirty(def);
            result.Add(def);
        }
        return result.ToArray();
    }

    // ---------------- 씬 ----------------

    private static void CreateScene(GameConfig config)
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
        Camera cam = cameraGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 3f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.06f, 0.05f, 0.05f);
        cameraGo.AddComponent<AudioListener>();
        cameraGo.transform.position = new Vector3(0f, 0f, -10f);
        cameraGo.AddComponent<CameraController>();

        // 전역 포스트프로세싱 Volume (프로필: Settings/PostProcess/GamePostProcessProfile)
        if (config.PostProcessProfile != null)
        {
            var volumeGo = new GameObject("Global Volume");
            var volume = volumeGo.AddComponent<UnityEngine.Rendering.Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = config.PostProcessProfile;
        }

        var game = new GameObject("Game");
        GameBootstrap bootstrap = game.AddComponent<GameBootstrap>();
        var so = new SerializedObject(bootstrap);
        so.FindProperty("config").objectReferenceValue = config;
        so.ApplyModifiedPropertiesWithoutUndo();

        const string scenePath = "Assets/_DungeonHunt/Scenes/Game.unity";
        EditorSceneManager.SaveScene(scene, scenePath);
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        scenes.RemoveAll(s => s.path == scenePath);
        scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    // ---------------- 공용 ----------------

    private static T CreateOrLoad<T>(string path) where T : ScriptableObject
    {
        T existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null) return existing;
        T created = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(created, path);
        return created;
    }

    public static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
