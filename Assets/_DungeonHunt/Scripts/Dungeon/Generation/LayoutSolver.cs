using System.Collections.Generic;
using UnityEngine;

// 격자 위 방 연결 구조를 실제 타일 좌표로 펼침.
//
// 열 폭은 그 열에서 제일 넓은 방, 행 높이는 그 행에서 제일 높은 방에 맞추고 열·행 사이에 간격 둠.
// 방은 자기 칸 안에서 이어진 이웃 쪽 끝에 붙임(한 축에 이웃이 한쪽만 있을 때). 양쪽이거나 없으면 가운데.
// 간격은 그 간격을 지나는 복도 중 꺾이는 게 있으면 꺾을 공간만큼, 전부 곧으면 최소 간격만.
// 복도는 문에서 곧게 나와서 간격 안에서만 한 번 꺾여 상대 문으로 들어감(Z자).
// 좌우 연결은 열 사이 간격, 상하 연결은 행 사이 간격만 쓰고 간격 교차 구역은 비워둠
// → 복도끼리, 복도랑 방이 겹치거나 붙을 일 없음.
public static class LayoutSolver
{
    // templates: 방 Id 순서대로 배정된 템플릿
    public static FloorBlueprint Solve(DungeonLayout layout, IReadOnlyList<RoomTemplateData> templates, IReadOnlyList<int> templateIndices, int minSpacing, WallStyle style)
    {
        // 간격 안에서 꺾일 때 필요한 공간: 복도 폭 + 꺾인 구간 양쪽 벽 + 꺾이기 전후 직선 1칸씩
        int doorWidth = MaxDoorwayWidth(templates);
        var bendSpacing = new[]
        {
            Mathf.Max(minSpacing, doorWidth + style.Side * 2 + 2),
            Mathf.Max(minSpacing, doorWidth + style.Top + style.Bottom + 2)
        };
        var bands = new[]
        {
            new Bands(layout, templates, axis: 0),
            new Bands(layout, templates, axis: 1)
        };

        // 칸 안 위치 (칸 시작 기준). 간격과 무관하게 먼저 정함.
        // x 위치는 좌우 복도 길이와 상하 복도 꺾임만, y 위치는 그 반대만 바꾸므로 축마다 따로 최적화
        int[] xs = OptimizeOffsets(layout, templates, bands[0], 0, bendSpacing[1] - minSpacing);
        int[] ys = OptimizeOffsets(layout, templates, bands[1], 1, bendSpacing[0] - minSpacing);
        var offsets = new Vector2Int[layout.Rooms.Count];
        for (int i = 0; i < offsets.Length; i++) offsets[i] = new Vector2Int(xs[i], ys[i]);

        // 간격별로 꺾이는 복도가 있는지 보고 간격 크기 정함. 같은 열(행)끼리라 칸 시작은 상쇄돼서 위치 차이만 보면 됨
        for (int axis = 0; axis < 2; axis++)
            bands[axis].SetGaps(minSpacing, bendSpacing[axis], gap => NeedsBend(layout, templates, offsets, axis, gap));

        var rooms = new List<PlacedRoom>(layout.Rooms.Count);
        foreach (RoomNode node in layout.Rooms)
        {
            var position = new Vector2Int(
                bands[0].Start(node.Cell.x) + offsets[node.Id].x,
                bands[1].Start(node.Cell.y) + offsets[node.Id].y);
            rooms.Add(new PlacedRoom(node, templateIndices[node.Id], templates[node.Id], position));
        }

        var blueprint = new FloorBlueprint(layout, rooms);
        foreach (RoomLink link in layout.Links)
            blueprint.AddCorridor(CarveCorridor(link, rooms[link.A.Id], rooms[link.B.Id], bands, style));

        BuildWalls(blueprint, style);
        return blueprint;
    }

    // 한 축의 칸 안 위치를 정함. 비용 = 이 축 방향 복도의 칸 안 길이 + 반대 축 복도가 꺾이면 bendPenalty.
    // 시작값(이웃 쪽 끝 붙이기)에서 방 Id 순서로 후보 위치를 바꿔 보며 비용이 줄면 채택, 몇 바퀴 반복 (결정적)
    private const int OptimizePasses = 4;

    private static int[] OptimizeOffsets(DungeonLayout layout, IReadOnlyList<RoomTemplateData> templates, Bands band, int axis, int bendPenalty)
    {
        int count = layout.Rooms.Count;
        var offsets = new int[count];
        var free = new int[count];
        foreach (RoomNode node in layout.Rooms)
        {
            free[node.Id] = band.Size(node.Cell[axis]) - templates[node.Id].Size[axis];
            offsets[node.Id] = AlignedOffset(node, axis, free[node.Id]);
        }

        for (int pass = 0; pass < OptimizePasses; pass++)
        {
            bool changed = false;
            foreach (RoomNode node in layout.Rooms)
            {
                int best = offsets[node.Id];
                int bestCost = RoomCost(node, best, offsets, templates, band, axis, bendPenalty);
                foreach (int candidate in Candidates(node, offsets, free, templates, axis))
                {
                    int cost = RoomCost(node, candidate, offsets, templates, band, axis, bendPenalty);
                    if (cost < bestCost) { bestCost = cost; best = candidate; }
                }
                if (best != offsets[node.Id]) { offsets[node.Id] = best; changed = true; }
            }
            if (!changed) break;
        }
        return offsets;
    }

    // 후보: 양 끝, 가운데, 반대 축 이웃과 문 위치가 맞는 자리
    private static IEnumerable<int> Candidates(RoomNode node, int[] offsets, int[] free, IReadOnlyList<RoomTemplateData> templates, int axis)
    {
        int max = free[node.Id];
        yield return 0;
        yield return max;
        yield return max / 2;
        foreach (RoomNode neighbor in node.Neighbors)
        {
            if (neighbor.Cell[axis] != node.Cell[axis]) continue; // 반대 축 연결만 (같은 열/행)
            RoomSide side = node.SideTowards(neighbor);
            int mine = templates[node.Id].GetDoorway(side).Start;
            int theirs = offsets[neighbor.Id] + templates[neighbor.Id].GetDoorway(side.Opposite()).Start;
            int aligned = theirs - mine;
            if (aligned >= 0 && aligned <= max) yield return aligned;
        }
    }

    private static int RoomCost(RoomNode node, int offset, int[] offsets, IReadOnlyList<RoomTemplateData> templates, Bands band, int axis, int bendPenalty)
    {
        int cost = 0;
        int size = templates[node.Id].Size[axis];
        foreach (RoomNode neighbor in node.Neighbors)
        {
            if (neighbor.Cell[1 - axis] == node.Cell[1 - axis])
            {
                // 이 축 방향 연결: 칸 끝까지 남는 길이가 복도 길이에 더해짐
                cost += neighbor.Cell[axis] > node.Cell[axis] ? band.Size(node.Cell[axis]) - offset - size : offset;
            }
            else
            {
                // 반대 축 연결: 문 구간이 폭만큼 안 겹치면 꺾임
                RoomSide side = node.SideTowards(neighbor);
                Doorway mine = templates[node.Id].GetDoorway(side);
                Doorway theirs = templates[neighbor.Id].GetDoorway(side.Opposite());
                int width = Mathf.Min(mine.Width, theirs.Width);
                ChooseLanes(offset + mine.Start, mine.Width, offsets[neighbor.Id] + theirs.Start, theirs.Width, width, out int laneA, out int laneB);
                if (laneA != laneB) cost += bendPenalty;
            }
        }
        return cost;
    }

    // 한 축에서 이어진 이웃이 한쪽에만 있으면 그쪽 칸 끝에 붙임 → 복도가 짧아짐
    private static int AlignedOffset(RoomNode node, int axis, int free)
    {
        bool low = false, high = false;
        foreach (RoomNode neighbor in node.Neighbors)
        {
            if (neighbor.Cell[1 - axis] != node.Cell[1 - axis]) continue;
            if (neighbor.Cell[axis] < node.Cell[axis]) low = true;
            else high = true;
        }
        if (low && !high) return 0;
        if (high && !low) return free;
        return free / 2;
    }

    // gap = 칸 gap과 gap+1 사이 간격. 그 간격을 지나는 복도 중 하나라도 꺾이면 true
    private static bool NeedsBend(DungeonLayout layout, IReadOnlyList<RoomTemplateData> templates, Vector2Int[] offsets, int main, int gap)
    {
        int cross = 1 - main;
        foreach (RoomLink link in layout.Links)
        {
            RoomNode a = link.A, b = link.B;
            if (a.Cell[cross] != b.Cell[cross]) continue;
            if (a.Cell[main] > b.Cell[main]) (a, b) = (b, a);
            if (a.Cell[main] != gap) continue;

            RoomSide sideA = a.SideTowards(b);
            Doorway doorA = templates[a.Id].GetDoorway(sideA);
            Doorway doorB = templates[b.Id].GetDoorway(sideA.Opposite());
            int width = Mathf.Min(doorA.Width, doorB.Width);
            ChooseLanes(offsets[a.Id][cross] + doorA.Start, doorA.Width, offsets[b.Id][cross] + doorB.Start, doorB.Width, width, out int laneA, out int laneB);
            if (laneA != laneB) return true;
        }
        return false;
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
    private static Corridor CarveCorridor(RoomLink link, PlacedRoom a, PlacedRoom b, Bands[] bands, WallStyle style)
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
            int slack = bands[main].GapAfter(a.Node.Cell[main]) - (width + lowWall + highWall + 2);
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

    // 격자 한 축(열 또는 행)의 칸별 시작 좌표랑 크기, 칸 사이 간격
    private class Bands
    {
        private readonly int minCell;
        private readonly int[] starts;
        private readonly int[] sizes;
        private readonly int[] gaps;

        public Bands(DungeonLayout layout, IReadOnlyList<RoomTemplateData> templates, int axis)
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
            gaps = new int[sizes.Length];
        }

        // needsBend(칸): 그 칸과 다음 칸 사이를 지나는 복도 중 꺾이는 게 있는지
        public void SetGaps(int straightGap, int bendGap, System.Func<int, bool> needsBend)
        {
            for (int i = 0; i < sizes.Length; i++)
                gaps[i] = needsBend(i + minCell) ? bendGap : straightGap;
            for (int i = 1; i < sizes.Length; i++)
                starts[i] = starts[i - 1] + sizes[i - 1] + gaps[i - 1];
        }

        public int Start(int cell) => starts[cell - minCell];
        public int Size(int cell) => sizes[cell - minCell];
        public int End(int cell) => Start(cell) + Size(cell);
        public int GapAfter(int cell) => gaps[cell - minCell];
    }
}
