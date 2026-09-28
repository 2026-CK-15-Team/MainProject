using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

// 방 프리팹 타일맵 읽어서 크기랑 문 정보를 Room에 저장함. 제작 규칙(RoomTemplateGuide.md) 어기면 경고 띄움.
public static class RoomBaker
{
    public static void Bake(Room room)
    {
        if (room.FloorTilemap == null || room.WallTilemap == null)
        {
            Debug.LogError($"{room.name}: Floor/Wall 타일맵 연결 안 됨", room);
            return;
        }

        var problems = new List<string>();

        if (!TryGetTileBounds(room, out RectInt bounds))
        {
            Debug.LogError($"{room.name}: 타일 없음", room);
            return;
        }
        if (bounds.min != Vector2Int.zero)
            problems.Add($"바운딩 왼쪽 아래가 (0, 0) 아님: {bounds.min}");

        Vector2Int size = bounds.max;
        var doorways = new List<Doorway>();
        foreach (RoomSide side in RoomSideUtility.All)
        {
            List<Doorway> runs = FindOpenRuns(room, size, side);
            if (runs.Count == 1)
                doorways.Add(runs[0]);
            else if (runs.Count > 1)
                problems.Add($"{side} 변에 문이 {runs.Count}개임. 한 변에 문은 1개만.");
        }

        ValidateDoorBarriers(room, doorways, problems);

        Undo.RecordObject(room, "Bake Room");
        room.Template.SetShape(size, doorways);
        EditorUtility.SetDirty(room);

        string doorSummary = string.Join(", ", doorways.Select(doorway => $"{doorway.Side}(시작 {doorway.Start}, 폭 {doorway.Width})"));
        Debug.Log($"{room.name} Bake 끝: 크기 {size}, 문 {doorSummary}", room);
        foreach (string problem in problems)
            Debug.LogWarning($"{room.name}: {problem}", room);
    }

    // 자식 타일맵 전체에서 실제 타일 있는 범위. Tilemap.cellBounds는 지운 칸까지 잡힐 수 있어서 직접 계산함.
    private static bool TryGetTileBounds(Room room, out RectInt bounds)
    {
        var min = new Vector2Int(int.MaxValue, int.MaxValue);
        var max = new Vector2Int(int.MinValue, int.MinValue);

        foreach (Tilemap tilemap in room.GetComponentsInChildren<Tilemap>(true))
        {
            foreach (Vector3Int position in tilemap.cellBounds.allPositionsWithin)
            {
                if (!tilemap.HasTile(position)) continue;

                min = Vector2Int.Min(min, (Vector2Int)position);
                max = Vector2Int.Max(max, (Vector2Int)position + Vector2Int.one);
            }
        }

        bounds = new RectInt(min, max - min);
        return max.x > min.x;
    }

    // 경계선 위에서 바닥 있고 벽 없는 칸이 이어진 구간
    private static List<Doorway> FindOpenRuns(Room room, Vector2Int size, RoomSide side)
    {
        int length = side == RoomSide.Up || side == RoomSide.Down ? size.x : size.y;
        var probe = new Doorway(side, 0, 1);
        var runs = new List<Doorway>();
        int runStart = -1;

        for (int i = 0; i <= length; i++)
        {
            bool isOpen = i < length && IsOpen(room, probe.GetCell(size, i));
            if (isOpen && runStart < 0)
            {
                runStart = i;
            }
            else if (!isOpen && runStart >= 0)
            {
                runs.Add(new Doorway(side, runStart, i - runStart));
                runStart = -1;
            }
        }
        return runs;
    }

    private static bool IsOpen(Room room, Vector2Int cell)
        => room.FloorTilemap.HasTile((Vector3Int)cell) && !room.WallTilemap.HasTile((Vector3Int)cell);

    private static void ValidateDoorBarriers(Room room, List<Doorway> doorways, List<string> problems)
    {
        RoomDoor[] doors = room.GetComponentsInChildren<RoomDoor>(true);
        foreach (Doorway doorway in doorways)
        {
            int count = doors.Count(door => door.Side == doorway.Side);
            if (count != 1)
                problems.Add($"{doorway.Side} 문 RoomDoor(장벽)가 {count}개임. 1개여야 함.");
        }
        foreach (RoomDoor door in doors)
        {
            if (!doorways.Exists(doorway => doorway.Side == door.Side))
                problems.Add($"{door.name}: {door.Side} 방향엔 문이 없는데 RoomDoor가 있음");
        }
    }
}
