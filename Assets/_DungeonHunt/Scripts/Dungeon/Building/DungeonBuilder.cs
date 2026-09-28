using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

// FloorBlueprint를 씬에 실제로 깔아줌. 방 프리팹 생성 + 복도는 공용 타일맵에 칠함.
public class DungeonBuilder : MonoBehaviour
{
    [SerializeField] private RunConfig runConfig;

    [Tooltip("복도 타일맵이 있는 Grid. 방 프리팹이랑 셀 크기 같아야 함.")]
    [SerializeField] private Grid grid;
    [SerializeField] private Tilemap corridorFloorTilemap;
    [SerializeField] private Tilemap corridorWallTilemap;
    [SerializeField] private Transform roomRoot;

    [Header("테스트")]
    [SerializeField] private int testRunSeed;
    [SerializeField, Min(0)] private int testFloorIndex;

    private readonly List<Room> rooms = new List<Room>();

    public IReadOnlyList<Room> Rooms => rooms;
    public Room StartRoom { get; private set; }

    public void Build(int runSeed, int floorIndex)
    {
        Clear();

        FloorConfig floor = runConfig.Floors[floorIndex];
        FloorPlan plan = RunPlanner.Plan(runConfig, runSeed)[floorIndex];
        List<RoomTemplateData> templates = floor.RoomPrefabs.Select(prefab => prefab.Template).ToList();
        FloorBlueprint blueprint = FloorGenerator.Generate(plan, templates, floor.MinRoomSpacing);

        foreach (PlacedRoom placed in blueprint.Rooms)
        {
            Vector3 position = grid.CellToWorld((Vector3Int)placed.Position);
            Room room = Instantiate(floor.RoomPrefabs[placed.TemplateIndex], position, Quaternion.identity, roomRoot);
            room.name = $"{placed.Node.Type}_{placed.Node.Id}";
            room.Initialize(placed.Node, placed.GetSealedCells(), floor.WallTile);
            rooms.Add(room);

            if (placed.Node.Type == RoomType.Start)
                StartRoom = room;
        }

        Paint(corridorFloorTilemap, blueprint.CorridorFloor, floor.CorridorFloorTile);
        Paint(corridorWallTilemap, blueprint.CorridorWalls, floor.WallTile);
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
        StartRoom = null;

        corridorFloorTilemap.ClearAllTiles();
        corridorWallTilemap.ClearAllTiles();
    }

    [ContextMenu("Build Test Floor")]
    private void BuildTestFloor() => Build(testRunSeed, testFloorIndex);

    [ContextMenu("Clear")]
    private void ClearFromMenu() => Clear();

    private static void Paint(Tilemap tilemap, ICollection<Vector2Int> cells, TileBase tile)
    {
        Vector3Int[] positions = cells.Select(cell => (Vector3Int)cell).ToArray();
        TileBase[] tiles = Enumerable.Repeat(tile, positions.Length).ToArray();
        tilemap.SetTiles(positions, tiles);
    }
}
