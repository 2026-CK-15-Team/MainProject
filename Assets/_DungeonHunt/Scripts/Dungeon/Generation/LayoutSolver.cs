using System.Collections.Generic;
using UnityEngine;

// 격자 위 방 연결 구조를 실제 타일 좌표로 펼침.
//
// 열 폭은 그 열에서 제일 넓은 방, 행 높이는 그 행에서 제일 높은 방에 맞추고 열·행 사이에 간격 둠.
// 방은 자기 칸 가운데에 놓임. 복도는 문에서 곧게 나와서 간격 안에서만 한 번 꺾여 상대 문으로 들어감(Z자).
// 좌우 연결은 열 사이 간격, 상하 연결은 행 사이 간격만 쓰고 간격 교차 구역은 비워둠
// → 복도끼리, 복도랑 방이 겹치거나 붙을 일 없음.
public static class LayoutSolver
{
    // templates: 방 Id 순서대로 배정된 템플릿
    public static FloorBlueprint Solve(DungeonLayout layout, IReadOnlyList<RoomTemplateData> templates, IReadOnlyList<int> templateIndices, int minSpacing, WallStyle style)
    {
        // 간격 안에서 꺾일 때 필요한 공간: 복도 폭 + 꺾인 구간 양쪽 벽 + 꺾이기 전후 직선 1칸씩
        int doorWidth = MaxDoorwayWidth(templates);
        var spacing = new[]
        {
            Mathf.Max(minSpacing, doorWidth + style.Side * 2 + 2),
            Mathf.Max(minSpacing, doorWidth + style.Top + style.Bottom + 2)
        };
        var bands = new[]
        {
            new Bands(layout, templates, axis: 0, spacing[0]),
            new Bands(layout, templates, axis: 1, spacing[1])
        };

        var rooms = new List<PlacedRoom>(layout.Rooms.Count);
        foreach (RoomNode node in layout.Rooms)
        {
            RoomTemplateData template = templates[node.Id];
            var position = new Vector2Int(
                bands[0].Start(node.Cell.x) + (bands[0].Size(node.Cell.x) - template.Size.x) / 2,
                bands[1].Start(node.Cell.y) + (bands[1].Size(node.Cell.y) - template.Size.y) / 2);
            rooms.Add(new PlacedRoom(node, templateIndices[node.Id], template, position));
        }

        var blueprint = new FloorBlueprint(layout, rooms);
        foreach (RoomLink link in layout.Links)
            blueprint.AddCorridor(CarveCorridor(link, rooms[link.A.Id], rooms[link.B.Id], bands, spacing, style));

        BuildWalls(blueprint, style);
        return blueprint;
    }

    private static int MaxDoorwayWidth(IReadOnlyList<RoomTemplateData> templates)
    {
        int max = 0;
        foreach (RoomTemplateData template in templates)
        {
            foreach (Doorway doorway in template.Doorways)
                max = Mathf.Max(max, doorway.Width);
        }
        return max;
    }

    // 좌우/상하 연결을 같은 코드로 처리하려고 main(연결 방향), cross(수직 방향) 축으로 계산함.
    private static Corridor CarveCorridor(RoomLink link, PlacedRoom a, PlacedRoom b, Bands[] bands, int[] spacing, WallStyle style)
    {
        int main = a.Node.Cell.x != b.Node.Cell.x ? 0 : 1;
        int cross = 1 - main;
        if (a.Node.Cell[main] > b.Node.Cell[main])
            (a, b) = (b, a);

        RoomSide sideA = a.Node.SideTowards(b.Node);
        RoomSide sideB = sideA.Opposite();
        Doorway doorA = a.Template.GetDoorway(sideA);
        Doorway doorB = b.Template.GetDoorway(sideB);
        int width = Mathf.Min(doorA.Width, doorB.Width);

        int doorStartA = a.Position[cross] + doorA.Start;
        int doorStartB = b.Position[cross] + doorB.Start;
        ChooseLanes(doorStartA, doorA.Width, doorStartB, doorB.Width, width, out int laneA, out int laneB);
        a.OpenDoorway(sideA, laneA - a.Position[cross], width);
        b.OpenDoorway(sideB, laneB - b.Position[cross], width);

        int from = a.Position[main] + a.Template.Size[main];
        int to = b.Position[main] - 1;
        var cells = new List<Vector2Int>();

        if (laneA == laneB)
        {
            AddRect(cells, main, from, to, laneA, laneA + width - 1);
        }
        else
        {
            // 꺾인 구간의 a 쪽 벽(low), b 쪽 벽(high)이 간격 안에 들어가게 남는 공간 가운데에 둠.
            // 상하 연결이면 꺾인 구간이 가로라서 아래 벽이 a 쪽, 위 벽이 b 쪽.
            int lowWall = main == 0 ? style.Side : style.Bottom;
            int highWall = main == 0 ? style.Side : style.Top;
            int slack = spacing[main] - (width + lowWall + highWall + 2);
            int bend = bands[main].End(a.Node.Cell[main]) + lowWall + 1 + slack / 2;

            AddRect(cells, main, from, bend - 1, laneA, laneA + width - 1);
            AddRect(cells, main, bend, bend + width - 1, Mathf.Min(laneA, laneB), Mathf.Max(laneA, laneB) + width - 1);
            AddRect(cells, main, bend + width, to, laneB, laneB + width - 1);
        }
        return new Corridor(link, cells);
    }

    // 두 문에서 복도 폭만큼 구간(lane) 고름. 겹치는 구간 충분하면 그 가운데로 곧게 잇고,
    // 아니면 각 문에서 상대 문에 제일 가까운 구간 골라서 꺾이는 거리 줄임.
    private static void ChooseLanes(int startA, int widthA, int startB, int widthB, int width, out int laneA, out int laneB)
    {
        int overlapStart = Mathf.Max(startA, startB);
        int overlapEnd = Mathf.Min(startA + widthA, startB + widthB);
        if (overlapEnd - overlapStart >= width)
        {
            laneA = laneB = overlapStart + (overlapEnd - overlapStart - width) / 2;
            return;
        }

        laneA = Mathf.Clamp(startB, startA, startA + widthA - width);
        laneB = Mathf.Clamp(laneA, startB, startB + widthB - width);
    }

    private static void AddRect(List<Vector2Int> cells, int main, int mainFrom, int mainTo, int crossFrom, int crossTo)
    {
        for (int m = mainFrom; m <= mainTo; m++)
        {
            for (int c = crossFrom; c <= crossTo; c++)
                cells.Add(main == 0 ? new Vector2Int(m, c) : new Vector2Int(c, m));
        }
    }

    // 복도 바닥 둘레에 방향별 두께만큼 벽 두름. 방 바운딩 안쪽은 방 자체 벽 쓰니까 제외.
    private static void BuildWalls(FloorBlueprint blueprint, WallStyle style)
    {
        HashSet<Vector2Int> floor = blueprint.CorridorFloor;
        var wallCells = new HashSet<Vector2Int>();

        foreach (Vector2Int cell in floor)
        {
            for (int dx = -style.Side; dx <= style.Side; dx++)
            {
                for (int dy = -style.Bottom; dy <= style.Top; dy++)
                {
                    Vector2Int neighbor = cell + new Vector2Int(dx, dy);
                    if (!floor.Contains(neighbor) && !blueprint.IsInsideRoom(neighbor))
                        wallCells.Add(neighbor);
                }
            }
        }

        foreach (Vector2Int cell in wallCells)
        {
            WallPart part = Classify(cell, floor, style);
            bool isFront = part == WallPart.Bottom && HasFloorInLine(floor, cell, Vector2Int.up, style.FrontRowCount);
            blueprint.CorridorWalls.Add(new WallCell(cell, part, isFront));
        }
    }

    // 바닥이 바로 아래 있으면 위쪽 벽, 바로 위에 있으면 아래쪽 벽, 옆에 있으면 좌우 벽.
    // 모서리(대각선에만 바닥)는 3/4 시점처럼 위·아래 벽이 좌우 벽 끝을 덮게 위/아래 벽으로 처리.
    private static WallPart Classify(Vector2Int cell, HashSet<Vector2Int> floor, WallStyle style)
    {
        if (HasFloorInLine(floor, cell, Vector2Int.down, style.Top)) return WallPart.Top;
        if (HasFloorInLine(floor, cell, Vector2Int.up, style.Bottom)) return WallPart.Bottom;
        if (HasFloorInLine(floor, cell, Vector2Int.left, style.Side) || HasFloorInLine(floor, cell, Vector2Int.right, style.Side))
            return WallPart.Side;

        for (int dy = 1; dy <= style.Top; dy++)
        {
            for (int dx = -style.Side; dx <= style.Side; dx++)
            {
                if (floor.Contains(cell + new Vector2Int(dx, -dy)))
                    return WallPart.Top;
            }
        }
        return WallPart.Bottom;
    }

    private static bool HasFloorInLine(HashSet<Vector2Int> floor, Vector2Int from, Vector2Int direction, int distance)
    {
        for (int i = 1; i <= distance; i++)
        {
            if (floor.Contains(from + direction * i))
                return true;
        }
        return false;
    }

    // 격자 한 축(열 또는 행)의 칸별 시작 좌표랑 크기
    private class Bands
    {
        private readonly int minCell;
        private readonly int[] starts;
        private readonly int[] sizes;

        public Bands(DungeonLayout layout, IReadOnlyList<RoomTemplateData> templates, int axis, int spacing)
        {
            minCell = int.MaxValue;
            int maxCell = int.MinValue;
            foreach (RoomNode room in layout.Rooms)
            {
                minCell = Mathf.Min(minCell, room.Cell[axis]);
                maxCell = Mathf.Max(maxCell, room.Cell[axis]);
            }

            sizes = new int[maxCell - minCell + 1];
            foreach (RoomNode room in layout.Rooms)
            {
                int index = room.Cell[axis] - minCell;
                sizes[index] = Mathf.Max(sizes[index], templates[room.Id].Size[axis]);
            }

            starts = new int[sizes.Length];
            for (int i = 1; i < sizes.Length; i++)
                starts[i] = starts[i - 1] + sizes[i - 1] + spacing;
        }

        public int Start(int cell) => starts[cell - minCell];
        public int Size(int cell) => sizes[cell - minCell];
        public int End(int cell) => Start(cell) + Size(cell);
    }
}
