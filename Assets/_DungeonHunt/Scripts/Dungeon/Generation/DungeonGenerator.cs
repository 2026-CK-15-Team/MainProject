using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// FloorPlan으로 한 층 방 연결 구조 만듦. FloorPlan 같으면 결과도 항상 같음.
//
// 순서: 전투방 뼈대 → 전투방 N개까지 가지 뻗기 → 시작방 → 보스·계단 → 나머지 특수방
// - 새 방은 늘 빈 칸에 놓고 기존 방 하나랑만 이어서 순환이 안 생김.
// - 순환 층은 뼈대를 고리로 시작함 → 그 고리가 층의 유일한 순환.
// - 특수방은 전투방에 한 번만 이어서 항상 말단.
public static class DungeonGenerator
{
    public const int MinLoopRoomCount = 4;
    private const int MaxAttempts = 100;

    // isAcceptable: 규칙 외 추가 조건(예: 모든 방에 맞는 템플릿 있는지). 통과 못 하면 다시 뽑음.
    public static DungeonLayout Generate(FloorPlan plan, Predicate<DungeonLayout> isAcceptable = null)
    {
        if (plan.HasLoop && plan.CombatRoomCount < MinLoopRoomCount)
            throw new ArgumentException($"{plan.FloorIndex + 1}층: 순환 있는 층은 전투방 {MinLoopRoomCount}개 이상 필요");

        // 실패하면 시도 번호로 시드 바꿔서 재시도. 재시도도 결정적이라 재현성 그대로임.
        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var rng = new System.Random(SeedUtility.Combine(plan.Seed, attempt));
            var layout = new DungeonLayout(plan);
            if (TryBuild(layout, rng) && (isAcceptable == null || isAcceptable(layout)))
                return layout;
        }
        throw new InvalidOperationException($"{plan.FloorIndex + 1}층 생성 실패 (시드 {plan.Seed})");
    }

    private static bool TryBuild(DungeonLayout layout, System.Random rng)
    {
        FloorPlan plan = layout.Plan;

        List<RoomNode> combatRooms = plan.HasLoop
            ? CreateCombatLoop(layout, plan.CombatRoomCount, rng)
            : new List<RoomNode> { layout.AddRoom(RoomType.Combat, Vector2Int.zero) };

        while (combatRooms.Count < plan.CombatRoomCount)
        {
            if (!TryAttachRoom(layout, combatRooms, RoomType.Combat, rng, out RoomNode room)) return false;
            combatRooms.Add(room);
        }

        if (!TryAttachRoom(layout, combatRooms, RoomType.Start, rng, out RoomNode start)) return false;
        if (!TryAttachBoss(layout, combatRooms, start, plan.HasStairs, rng)) return false;

        foreach (RoomType type in plan.LeafRooms)
        {
            if (!TryAttachRoom(layout, combatRooms, type, rng, out _)) return false;
        }
        return true;
    }

    // 전투방 수 안에 들어가는 사각형 테두리 하나 골라서 고리로 이음.
    private static List<RoomNode> CreateCombatLoop(DungeonLayout layout, int maxRoomCount, System.Random rng)
    {
        var sizes = new List<Vector2Int>();
        for (int width = 2; LoopLength(width, 2) <= maxRoomCount; width++)
        {
            for (int height = 2; LoopLength(width, height) <= maxRoomCount; height++)
                sizes.Add(new Vector2Int(width, height));
        }

        var loop = new List<RoomNode>();
        foreach (Vector2Int cell in GetLoopCells(rng.Pick(sizes)))
            loop.Add(layout.AddRoom(RoomType.Combat, cell));

        for (int i = 0; i < loop.Count; i++)
            layout.Link(loop[i], loop[(i + 1) % loop.Count]);

        return loop;
    }

    private static int LoopLength(int width, int height) => 2 * (width + height) - 4;

    // (0,0)부터 테두리 한 바퀴 도는 순서. 앞뒤 칸(마지막↔첫 칸 포함)은 격자에서 맞닿음.
    private static IEnumerable<Vector2Int> GetLoopCells(Vector2Int size)
    {
        for (int x = 0; x < size.x; x++) yield return new Vector2Int(x, 0);
        for (int y = 1; y < size.y; y++) yield return new Vector2Int(size.x - 1, y);
        for (int x = size.x - 2; x >= 0; x--) yield return new Vector2Int(x, size.y - 1);
        for (int y = size.y - 2; y >= 1; y--) yield return new Vector2Int(0, y);
    }

    // hosts 중 하나의 빈 이웃 칸에 새 방 놓고 그 host랑만 연결함.
    // 다른 방이랑 안 붙는 칸을 우선함 → 방이 안 뭉치고 가지처럼 뻗음.
    private static bool TryAttachRoom(DungeonLayout layout, List<RoomNode> hosts, RoomType type, System.Random rng, out RoomNode room)
    {
        var candidates = new List<(RoomNode host, Vector2Int cell)>();
        var isolatedCandidates = new List<(RoomNode host, Vector2Int cell)>();

        foreach (RoomNode host in hosts)
        {
            foreach (RoomSide side in RoomSideUtility.All)
            {
                Vector2Int cell = host.Cell + side.ToOffset();
                if (layout.IsOccupied(cell)) continue;

                candidates.Add((host, cell));
                if (layout.CountOccupiedNeighbors(cell) == 1)
                    isolatedCandidates.Add((host, cell));
            }
        }

        if (candidates.Count == 0)
        {
            room = null;
            return false;
        }

        (RoomNode chosenHost, Vector2Int chosenCell) = rng.Pick(isolatedCandidates.Count > 0 ? isolatedCandidates : candidates);
        room = layout.AddRoom(type, chosenCell);
        layout.Link(chosenHost, room);
        return true;
    }

    // 시작방에서 먼 전투방부터 보스방 자리 찾음. 계단방 있으면 보스방 너머에 빈 칸 하나 더 필요.
    private static bool TryAttachBoss(DungeonLayout layout, List<RoomNode> combatRooms, RoomNode start, bool hasStairs, System.Random rng)
    {
        Dictionary<RoomNode, int> distances = layout.GetDistancesFrom(start);

        // 거리 같은 방끼리는 섞은 순서 유지(OrderBy는 안정 정렬).
        var hosts = new List<RoomNode>(combatRooms);
        rng.Shuffle(hosts);

        foreach (RoomNode host in hosts.OrderByDescending(room => distances[room]))
        {
            var options = new List<(Vector2Int boss, Vector2Int stairs)>();
            foreach (RoomSide bossSide in RoomSideUtility.All)
            {
                Vector2Int bossCell = host.Cell + bossSide.ToOffset();
                if (layout.IsOccupied(bossCell)) continue;

                if (!hasStairs)
                {
                    options.Add((bossCell, default));
                    continue;
                }

                foreach (RoomSide stairsSide in RoomSideUtility.All)
                {
                    Vector2Int stairsCell = bossCell + stairsSide.ToOffset();
                    if (!layout.IsOccupied(stairsCell))
                        options.Add((bossCell, stairsCell));
                }
            }

            if (options.Count == 0) continue;

            (Vector2Int chosenBoss, Vector2Int chosenStairs) = rng.Pick(options);
            RoomNode boss = layout.AddRoom(RoomType.Boss, chosenBoss);
            layout.Link(host, boss);

            if (hasStairs)
                layout.Link(boss, layout.AddRoom(RoomType.Stairs, chosenStairs));

            return true;
        }
        return false;
    }
}
