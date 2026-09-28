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

    [Tooltip("충돌 있는 벽")]
    [SerializeField] private Tilemap corridorWallTilemap;

    [Tooltip("아래 벽 중 캐릭터를 가리는 칸. 충돌 없음, 캐릭터보다 위에 그림")]
    [SerializeField] private Tilemap corridorWallFrontTilemap;

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
        WallStyle style = runConfig.WallStyle;
        FloorPlan plan = RunPlanner.Plan(runConfig, runSeed)[floorIndex];
        List<RoomTemplateData> templates = floor.RoomPrefabs.Select(prefab => prefab.Template).ToList();
        FloorBlueprint blueprint = FloorGenerator.Generate(plan, templates, floor.MinRoomSpacing, style);

        foreach (PlacedRoom placed in blueprint.Rooms)
        {
            Vector3 position = grid.CellToWorld((Vector3Int)placed.Position);
            Room room = Instantiate(floor.RoomPrefabs[placed.TemplateIndex], position, Quaternion.identity, roomRoot);
            room.name = $"{placed.Node.Type}_{placed.Node.Id}";
            room.Initialize(placed.Node);
            PaintWalls(room.FloorTilemap, room.WallTilemap, room.WallFrontTilemap, placed.GetSealedCells(style), floor, frontFloorTile: null);
            rooms.Add(room);

            if (placed.Node.Type == RoomType.Start)
                StartRoom = room;
        }

        Vector3Int[] floorCells = blueprint.CorridorFloor.Select(cell => (Vector3Int)cell).ToArray();
        corridorFloorTilemap.SetTiles(floorCells, Enumerable.Repeat(floor.CorridorFloorTile, floorCells.Length).ToArray());
        PaintWalls(corridorFloorTilemap, corridorWallTilemap, corridorWallFrontTilemap, blueprint.CorridorWalls, floor, floor.CorridorFloorTile);
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
