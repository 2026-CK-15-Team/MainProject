using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Tilemaps;

// FloorBlueprint를 씬에 실제로 깔아줌. 방 프리팹 생성 + 복도는 공용 타일맵에 칠함.
public class DungeonBuilder : MonoBehaviour
{
    [SerializeField] private RunConfig runConfig;

    [Tooltip("복도 타일맵이 있는 Grid. 방 프리팹이랑 셀 크기 같아야 함.")]
    [SerializeField] private Grid grid;
    [SerializeField] private Tilemap corridorFloorTilemap;

    [Tooltip("충돌 있는 벽")]
    [SerializeField] private Tilemap corridorWallTilemap;

    [Tooltip("아래 벽 중 캐릭터를 가리는 칸. 충돌 없음, 캐릭터보다 위에 그림")]
    [SerializeField] private Tilemap corridorWallFrontTilemap;

    [SerializeField] private Transform roomRoot;

    [Header("테스트")]
    [SerializeField] private int testRunSeed;
    [SerializeField, Min(0)] private int testFloorIndex;

    private readonly List<Room> rooms = new List<Room>();
    private readonly List<SecretLink> secretLinks = new List<SecretLink>();

    public IReadOnlyList<Room> Rooms => rooms;
    public Room StartRoom { get; private set; }
    public RunConfig RunConfig => runConfig;
    public Grid Grid => grid;
    public FloorBlueprint Blueprint { get; private set; }
    public FloorConfig CurrentFloor { get; private set; }
    public IReadOnlyList<PlacedRoom> PlacedRooms => Blueprint != null ? Blueprint.Rooms : null;
    // 부모 룸 → 비밀방 문 자리. 부모 쪽 입구는 벽으로 칠해 두고 SecretWall이 파괴를 맡음 (03-5 §4)
    public IReadOnlyList<SecretLink> SecretLinks => secretLinks;

    public class SecretLink
    {
        public Room Parent;
        public Room Secret;
        public List<WallCell> SealedCells;
        public RoomDoor ParentDoor;
    }

    // 런타임에 코드로 만들 때 연결 (GameBootstrap)
    public void Configure(RunConfig config, Grid targetGrid, Tilemap floorMap, Tilemap wallMap, Tilemap wallFrontMap, Transform roomParent)
    {
        runConfig = config;
        grid = targetGrid;
        corridorFloorTilemap = floorMap;
        corridorWallTilemap = wallMap;
        corridorWallFrontTilemap = wallFrontMap;
        roomRoot = roomParent;
    }

    public bool IsConfigured => runConfig != null && grid != null && corridorFloorTilemap != null && corridorWallTilemap != null;

    // 다음 층 배치 미리 계산 (03 §6.2: 현재 층 FloorGenerated 뒤). 배치 계산은 순수 C#이라 백그라운드 스레드에서 돌림.
    private Task<FloorBlueprint> pending;
    private int pendingSeed;
    private int pendingFloor = -1;

    // 두 번째 UI 제작 폴백이면 도박방 생성 안 함 (03-4 §2)
    private static bool ExcludeGamble => Application.isPlaying && GameBalance.Current.SecretRoom.SecondUIFallback;

    public void Precompute(int runSeed, int floorIndex)
    {
        if (runConfig == null || runConfig.Floors == null || floorIndex < 0 || floorIndex >= runConfig.Floors.Length) return;
        FloorConfig floor = runConfig.Floors[floorIndex];
        WallStyle style = runConfig.WallStyle;
        FloorPlan plan = RunPlanner.Plan(runConfig, runSeed, ExcludeGamble)[floorIndex];
        List<RoomTemplateData> templates = floor.RoomPrefabs.Select(prefab => prefab.Template).ToList();
        int spacing = floor.MinRoomSpacing;
        pendingSeed = runSeed;
        pendingFloor = floorIndex;
        pending = Task.Run(() => FloorGenerator.Generate(plan, templates, spacing, style));
    }

    // 미리 계산 중인 층이면 끝났는지, 아니면 바로 생성 가능(true)
    public bool IsPrecomputeReady(int runSeed, int floorIndex)
        => pending == null || pendingSeed != runSeed || pendingFloor != floorIndex || pending.IsCompleted;

    public void CancelPrecompute()
    {
        pending = null;
        pendingFloor = -1;
    }

    private FloorBlueprint TakePrecomputed(int runSeed, int floorIndex)
    {
        if (pending == null || pendingSeed != runSeed || pendingFloor != floorIndex) return null;
        Task<FloorBlueprint> task = pending;
        CancelPrecompute();
        try
        {
            return task.Result;
        }
        catch (System.AggregateException e)
        {
            Debug.LogWarning($"다음 층 미리 계산 실패 → 다시 계산: {e.InnerException?.Message}");
            return null;
        }
    }

    // ---------------- 코드 오토타일 (FloorConfig.Tileset이 있을 때) ----------------
    // 층 전체 바닥 칸. 방 프리팹 바닥(벽/앞면 칸 제외) - 막은 문 자리 + 복도 바닥
    private readonly HashSet<Vector2Int> autoFloor = new HashSet<Vector2Int>();
    private Tilemap gapTilemap;
    private int autoSeed;

    public bool UsesAutoTile => CurrentFloor != null && CurrentFloor.Tileset != null;

    private void AddRoomFloor(Room room, Vector2Int origin)
    {
        Tilemap floorMap = room.FloorTilemap, wallMap = room.WallTilemap, frontMap = room.WallFrontTilemap;
        if (floorMap == null) return;
        BoundsInt bounds = floorMap.cellBounds;
        foreach (Vector3Int c in bounds.allPositionsWithin)
        {
            if (!floorMap.HasTile(c)) continue;
            if (wallMap != null && wallMap.HasTile(c)) continue;
            if (frontMap != null && frontMap.HasTile(c)) continue;
            autoFloor.Add(origin + (Vector2Int)c);
        }
    }

    private void RemoveCells(Vector2Int origin, IEnumerable<WallCell> cells)
    {
        foreach (WallCell c in cells) autoFloor.Remove(origin + c.Cell);
    }

    // 방 타일맵은 판정 데이터(문 찾기, 이동 격자, 미니맵)로만 남기고 그림/충돌은 공용 타일맵이 맡음
    private static void HideRoomTilemaps(Room room)
    {
        foreach (Tilemap map in new[] { room.FloorTilemap, room.WallTilemap, room.WallFrontTilemap })
        {
            if (map == null) continue;
            if (map.TryGetComponent(out TilemapRenderer renderer)) renderer.enabled = false;
            foreach (Collider2D col in map.GetComponents<Collider2D>()) col.enabled = false;
        }
    }

    private void ApplyAutoTile()
    {
        if (gapTilemap == null && grid != null)
        {
            var go = new GameObject("Gap");
            go.transform.SetParent(grid.transform, false);
            gapTilemap = go.AddComponent<Tilemap>();
            go.AddComponent<TilemapRenderer>().sortingOrder = -10;
        }
        DungeonAutoTiler.Map map = DungeonAutoTiler.Classify(autoFloor, runConfig.WallStyle, CurrentFloor.Tileset.VoidMargin);
        DungeonAutoTiler.Paint(map, CurrentFloor.Tileset, corridorFloorTilemap, corridorWallTilemap, corridorWallFrontTilemap, gapTilemap, autoSeed);
    }

    // 비밀 벽 파괴 등으로 막힌 칸이 다시 바닥이 됐을 때 (층 칸 좌표)
    public void OpenCells(IEnumerable<Vector2Int> worldCells)
    {
        if (!UsesAutoTile) return;
        foreach (Vector2Int c in worldCells) autoFloor.Add(c);
        ApplyAutoTile();
    }

    public void Build(int runSeed, int floorIndex)
    {
        Clear();

        FloorConfig floor = runConfig.Floors[floorIndex];
        WallStyle style = runConfig.WallStyle;
        FloorBlueprint blueprint = TakePrecomputed(runSeed, floorIndex);
        if (blueprint == null)
        {
            FloorPlan plan = RunPlanner.Plan(runConfig, runSeed, ExcludeGamble)[floorIndex];
            List<RoomTemplateData> templates = floor.RoomPrefabs.Select(prefab => prefab.Template).ToList();
            blueprint = FloorGenerator.Generate(plan, templates, floor.MinRoomSpacing, style);
        }
        Blueprint = blueprint;
        CurrentFloor = floor;
        bool auto = UsesAutoTile;
        autoSeed = runSeed * 31 + floorIndex;

        foreach (PlacedRoom placed in blueprint.Rooms)
        {
            Vector3 position = grid.CellToWorld((Vector3Int)placed.Position);
            Room room = Instantiate(floor.RoomPrefabs[placed.TemplateIndex], position, Quaternion.identity, roomRoot);
            room.name = $"{placed.Node.Type}_{placed.Node.Id}";
            room.Initialize(placed.Node);
            if (auto)
            {
                AddRoomFloor(room, placed.Position);
                RemoveCells(placed.Position, placed.GetSealedCells(style));
                HideRoomTilemaps(room);
            }
            PaintWalls(room.FloorTilemap, room.WallTilemap, room.WallFrontTilemap, placed.GetSealedCells(style), floor, frontFloorTile: null);
            rooms.Add(room);

            if (placed.Node.Type == RoomType.Start)
                StartRoom = room;
        }

        SealSecretDoorways(blueprint, style, floor);

        if (auto)
        {
            foreach (Vector2Int cell in blueprint.CorridorFloor) autoFloor.Add(cell);
            ApplyAutoTile();
            return;
        }

        Vector3Int[] floorCells = blueprint.CorridorFloor.Select(cell => (Vector3Int)cell).ToArray();
        corridorFloorTilemap.SetTiles(floorCells, Enumerable.Repeat(floor.CorridorFloorTile, floorCells.Length).ToArray());
        PaintWalls(corridorFloorTilemap, corridorWallTilemap, corridorWallFrontTilemap, blueprint.CorridorWalls, floor, floor.CorridorFloorTile);
    }

    // 비밀방과 이어진 부모 룸 입구를 벽으로 막음. 그림은 일반 벽 타일과 같음.
    private void SealSecretDoorways(FloorBlueprint blueprint, WallStyle style, FloorConfig floor)
    {
        secretLinks.Clear();
        foreach (PlacedRoom placed in blueprint.Rooms)
        {
            if (placed.Node.Type != RoomType.Secret || placed.Node.Neighbors.Count == 0) continue;
            RoomNode parentNode = placed.Node.Neighbors[0];
            PlacedRoom parentPlaced = blueprint.Rooms[parentNode.Id];
            RoomSide side = parentNode.SideTowards(placed.Node);
            Room parentRoom = rooms[parentNode.Id];
            List<WallCell> cells = parentPlaced.GetOpeningSealCells(side, style);
            if (UsesAutoTile) RemoveCells(parentPlaced.Position, cells);
            PaintWalls(parentRoom.FloorTilemap, parentRoom.WallTilemap, parentRoom.WallFrontTilemap, cells, floor, frontFloorTile: null);

            RoomDoor door = null;
            foreach (RoomDoor candidate in parentRoom.GetComponentsInChildren<RoomDoor>(true))
            {
                if (candidate.Side != side) continue;
                door = candidate;
                candidate.gameObject.SetActive(false);
            }
            secretLinks.Add(new SecretLink { Parent = parentRoom, Secret = rooms[placed.Node.Id], SealedCells = cells, ParentDoor = door });
        }
    }

    public void Clear()
    {
        foreach (Room room in rooms)
        {
            if (room == null) continue;

            if (Application.isPlaying)
                Destroy(room.gameObject);
            else
                DestroyImmediate(room.gameObject);
        }
        rooms.Clear();
        secretLinks.Clear();
        autoFloor.Clear();
        if (gapTilemap != null) gapTilemap.ClearAllTiles();
        StartRoom = null;
        Blueprint = null;

        corridorFloorTilemap.ClearAllTiles();
        corridorWallTilemap.ClearAllTiles();
        if (corridorWallFrontTilemap != null)
            corridorWallFrontTilemap.ClearAllTiles();
    }

    [ContextMenu("Build Test Floor")]
    private void BuildTestFloor() => Build(testRunSeed, testFloorIndex);

    [ContextMenu("Clear")]
    private void ClearFromMenu() => Clear();

    // 가리는 칸은 Front 타일맵에 칠하고 밑에 바닥 깔아줌(frontFloorTile이 null이면 원래 바닥 유지).
    // 나머지 벽 칸은 Wall 타일맵에 칠하고 바닥 지움.
    private static void PaintWalls(Tilemap floorMap, Tilemap wallMap, Tilemap frontMap, IEnumerable<WallCell> cells, FloorConfig floor, TileBase frontFloorTile)
    {
        foreach (WallCell wall in cells)
        {
            var position = (Vector3Int)wall.Cell;
            TileBase tile = floor.GetWallTile(wall);

            if (wall.IsFront)
            {
                (frontMap != null ? frontMap : wallMap).SetTile(position, tile);
                if (frontFloorTile != null)
                    floorMap.SetTile(position, frontFloorTile);
            }
            else
            {
                wallMap.SetTile(position, tile);
                floorMap.SetTile(position, null);
            }
        }
    }
}
