using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 생성 결과가 층 규칙 지키는지 검사함. 생성기랑 따로 둬서 생성 로직 바꿔도 규칙 확인은 그대로 가능.
public static class LayoutValidator
{
    public static List<string> Validate(DungeonLayout layout)
    {
        var errors = new List<string>();
        FloorPlan plan = layout.Plan;

        ValidateRoomCounts(layout, errors);

        foreach (RoomNode room in layout.Rooms)
        {
            bool validDoorCount = room.Type switch
            {
                RoomType.Combat => room.DoorCount > 0,
                RoomType.Boss => room.DoorCount == (plan.HasStairs ? 2 : 1),
                _ => room.DoorCount == 1
            };
            if (!validDoorCount)
                errors.Add($"{room.Type} 방({room.Cell}) 문 수가 {room.DoorCount}개임");

            if (room.Type == RoomType.Stairs && room.Neighbors.Any(neighbor => neighbor.Type != RoomType.Boss))
                errors.Add($"계단방({room.Cell})이 보스방 말고 다른 방에 붙어 있음");
        }

        RoomNode start = layout.Rooms.FirstOrDefault(room => room.Type == RoomType.Start);
        if (start != null && layout.GetDistancesFrom(start).Count != layout.Rooms.Count)
            errors.Add("시작방에서 못 가는 방 있음");

        // 연결 그래프의 독립 순환 수 = 간선 - 정점 + 1
        int loopCount = layout.Links.Count - layout.Rooms.Count + 1;
        int expectedLoops = plan.HasLoop ? 1 : 0;
        if (loopCount != expectedLoops)
            errors.Add($"순환 {loopCount}개 (기대값 {expectedLoops})");

        return errors;
    }

    // 복도가 방 안을 뚫거나 다른 복도랑 벽 두께보다 가까우면 사이에 벽이 제대로 안 들어감.
    public static List<string> ValidateBlueprint(FloorBlueprint blueprint, WallStyle style)
    {
        var errors = new List<string>();
        var ownerByCell = new Dictionary<Vector2Int, Corridor>();

        foreach (Corridor corridor in blueprint.Corridors)
        {
            foreach (Vector2Int cell in corridor.Cells)
            {
                if (blueprint.IsInsideRoom(cell))
                    errors.Add($"복도가 방 안을 지나감: {cell}");
                ownerByCell[cell] = corridor;
            }
        }

        foreach (Corridor corridor in blueprint.Corridors)
        {
            foreach (Vector2Int cell in corridor.Cells)
            {
                if (HasOtherCorridorNearby(cell, corridor, ownerByCell, style))
                    errors.Add($"복도끼리 너무 붙어 있음: {cell}");
            }
        }
        return errors;
    }

    private static bool HasOtherCorridorNearby(Vector2Int cell, Corridor corridor, Dictionary<Vector2Int, Corridor> ownerByCell, WallStyle style)
    {
        for (int dx = -style.Side; dx <= style.Side; dx++)
        {
            for (int dy = -style.Bottom; dy <= style.Top; dy++)
            {
                if (ownerByCell.TryGetValue(cell + new Vector2Int(dx, dy), out Corridor other) && other != corridor)
                    return true;
            }
        }
        return false;
    }

    private static void ValidateRoomCounts(DungeonLayout layout, List<string> errors)
    {
        FloorPlan plan = layout.Plan;

        var expected = new Dictionary<RoomType, int>
        {
            [RoomType.Combat] = plan.CombatRoomCount,
            [RoomType.Start] = 1,
            [RoomType.Boss] = 1,
            [RoomType.Stairs] = plan.HasStairs ? 1 : 0
        };
        foreach (RoomType type in plan.LeafRooms)
            expected[type] = expected.TryGetValue(type, out int count) ? count + 1 : 1;

        foreach (RoomType type in Enum.GetValues(typeof(RoomType)))
        {
            int actual = layout.Rooms.Count(room => room.Type == type);
            int wanted = expected.TryGetValue(type, out int count) ? count : 0;
            if (actual != wanted)
                errors.Add($"{type} 방 {actual}개 (기대값 {wanted})");
        }
    }
}
