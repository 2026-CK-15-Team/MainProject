using System.Collections.Generic;
using UnityEngine;

// 기획서 룸 종류. 생성기의 RoomType을 층 위치에 따라 매핑함.
// Boss: 1, 2층 = EliteGateRoom / 마지막 층 = FinalBossRoom
// Stairs: 1, 2층 = FloorExitRoom / 마지막 층 = RunEndTreasureRoom
public enum RoomRole
{
    Start,
    Combat,
    Treasure,
    Shop,
    Gamble,
    Secret,
    EliteGate,
    FloorExit,
    FinalBoss,
    RunEndTreasure,
}

// 배치된 방 하나의 런타임 상태. FloorDirector가 생성 직후 붙임.
public class RoomRuntime : MonoBehaviour
{
    public Room Room { get; private set; }
    public RoomNode Node => Room != null ? Room.Node : null;
    public RoomRole Role { get; private set; }
    public int FloorIndex { get; private set; }
    public bool Visited { get; private set; }
    public RoomContent Content { get; private set; }
    public RectInt CellBounds { get; private set; }      // 층 칸 좌표의 바운딩
    public Rect InteriorRect { get; private set; }       // 바닥 경계 (벽 두께 안쪽), 월드 좌표
    public Transform MonsterRoot { get; private set; }
    public Transform PickupRoot { get; private set; }
    public List<RoomRuntime> Neighbors { get; } = new List<RoomRuntime>();
    public RoomRuntime SecretParent { get; set; }        // 비밀방이면 부모 룸

    private RoomNavGrid nav;
    public RoomNavGrid Nav => nav ??= new RoomNavGrid(Room);

    // 실제 바닥 모양 (바닥 타일 있고 벽 타일 없는 칸), 층 칸 좌표. 미니맵 모양과 문 틈 표시에 씀 (08 §4.1)
    private List<Vector2Int> floorCells;
    public IReadOnlyList<Vector2Int> FloorCells => floorCells ??= ComputeFloorCells();

    private List<Vector2Int> ComputeFloorCells()
    {
        var cells = new List<Vector2Int>();
        UnityEngine.Tilemaps.Tilemap floor = Room.FloorTilemap, wall = Room.WallTilemap;
        for (int x = 0; x < CellBounds.width; x++)
            for (int y = 0; y < CellBounds.height; y++)
            {
                var local = new Vector3Int(x, y, 0);
                if (floor != null && floor.HasTile(local) && (wall == null || !wall.HasTile(local)))
                    cells.Add(CellBounds.position + new Vector2Int(x, y));
            }
        return cells;
    }

    public bool IsCombatRoom => Role == RoomRole.Combat || Role == RoomRole.EliteGate || Role == RoomRole.FinalBoss;
    // 잠긴 전투룸/보스룸이면 확대 미니맵, 보유 아티팩트 UI, 순간이동 차단 (08 §5.1, §5.2)
    public bool IsLocked => Content != null && Content.IsLocked;
    public bool BlocksModal => IsLocked || (Content != null && Content.BlocksModal);

    public Vector2 Center => InteriorRect.center;

    public void Initialize(Room room, RoomRole role, int floorIndex, WallStyle style)
    {
        Room = room;
        Role = role;
        FloorIndex = floorIndex;

        Vector2Int size = room.Template.Size;
        Vector2Int origin = Vector2Int.RoundToInt(room.transform.position);
        CellBounds = new RectInt(origin, size);
        InteriorRect = new Rect(
            origin.x + style.Side,
            origin.y + style.Bottom,
            Mathf.Max(1, size.x - style.Side * 2),
            Mathf.Max(1, size.y - style.Bottom - style.Top));

        MonsterRoot = new GameObject("Monsters").transform;
        MonsterRoot.SetParent(transform, false);
        PickupRoot = new GameObject("Pickups").transform;
        PickupRoot.SetParent(transform, false);
    }

    public void SetContent(RoomContent content)
    {
        Content = content;
        content.Bind(this);
    }

    // 진입선: 바닥 경계에서 몸체 반경만큼 안쪽 (03 §5 RoomEntered)
    public bool ContainsForEntry(Vector2 position, float bodyRadius)
    {
        Rect r = InteriorRect;
        return position.x >= r.xMin + bodyRadius && position.x <= r.xMax - bodyRadius
            && position.y >= r.yMin + bodyRadius && position.y <= r.yMax - bodyRadius;
    }

    // 복도 진입선: 바닥 경계에서 몸체 반경만큼 바깥 (03 §5 CorridorEntered)
    public bool IsOutsideForCorridor(Vector2 position, float bodyRadius)
    {
        Rect r = InteriorRect;
        return position.x < r.xMin - bodyRadius || position.x > r.xMax + bodyRadius
            || position.y < r.yMin - bodyRadius || position.y > r.yMax + bodyRadius;
    }

    public bool ContainsCell(Vector2Int cell) => CellBounds.Contains(cell);

    // 카메라 경계: 외접 사각형 + 벽 (02 §3.2)
    public Rect CameraBounds => new Rect(CellBounds.x, CellBounds.y, CellBounds.width, CellBounds.height);

    public void MarkVisited() => Visited = true;

    public void OnEntered(bool first)
    {
        if (first) MarkVisited();
        Content?.OnPlayerEntered(first);
    }

    // ---------- 고정 지점 ----------

    public IEnumerable<RoomPoint> Points(RoomPointKind kind)
    {
        foreach (RoomPoint point in Room.GetComponentsInChildren<RoomPoint>(true))
            if (point.Kind == kind) yield return point;
    }

    public Vector2 GetPoint(RoomPointKind kind, RoomObjectRole role = RoomObjectRole.Default)
    {
        RoomPoint fallback = null;
        foreach (RoomPoint point in Points(kind))
        {
            if (point.Role == role) return point.transform.position;
            fallback ??= point;
        }
        if (fallback != null && role == RoomObjectRole.Default) return fallback.transform.position;
        if (kind != RoomPointKind.TeleportArrival)
            Debug.LogWarning($"{name}: {kind}({role}) 지점 없음. 룸 중앙 사용 (레이아웃 검수 대상, 03 §8.5)", this);
        return SafeCenter();
    }

    public Dictionary<int, Vector2> SpawnPoints()
    {
        var result = new Dictionary<int, Vector2>();
        foreach (RoomPoint point in Points(RoomPointKind.MonsterSpawn))
            result[point.Index] = point.transform.position;
        return result;
    }

    public Vector2 TeleportArrival => GetPoint(RoomPointKind.TeleportArrival);

    // 지점이 없을 때: 중앙에서 가장 가까운 걸을 수 있는 칸
    public Vector2 SafeCenter()
    {
        RoomNavGrid grid = Nav;
        if (grid.TryGetNearestWalkable(grid.WorldToCell(Center), out Vector2Int cell))
            return grid.CellCenter(cell);
        return Center;
    }

    // ---------- 문 잠금 ----------

    public void SetDoorsLocked(bool locked)
    {
        foreach (RoomDoor door in Room.GetComponentsInChildren<RoomDoor>(false))
            door.SetLocked(locked);
    }

    public void ClearTransientObjects()
    {
        foreach (Transform child in PickupRoot) Destroy(child.gameObject);
        foreach (Transform child in MonsterRoot) Destroy(child.gameObject);
    }
}
