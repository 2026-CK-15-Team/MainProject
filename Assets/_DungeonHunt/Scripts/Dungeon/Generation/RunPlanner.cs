using System.Collections.Generic;

// 런 시드로 층별 계획 정함. 여러 층에 걸친 규칙(도박방·비밀방 개수)은 여기서 처리.
public static class RunPlanner
{
    // excludeGamble: 두 번째 UI 제작 폴백 (03 §6.1.3). 도박방 0개, 빈 방으로 대체하지 않음.
    // 추첨 순서는 그대로 둬서 비밀방 개수/층 배정은 폴백이 없을 때와 같음.
    public static List<FloorPlan> Plan(RunConfig config, int runSeed, bool excludeGamble = false)
    {
        var plans = new List<FloorPlan>(config.Floors.Length);
        for (int i = 0; i < config.Floors.Length; i++)
        {
            FloorConfig floor = config.Floors[i];
            var plan = new FloorPlan(i, SeedUtility.Combine(runSeed, i), floor.CombatRoomCount, floor.HasLoop, floor.HasStairs);
            plan.LeafRooms.Add(RoomType.Treasure);
            plan.LeafRooms.Add(RoomType.Shop);
            plans.Add(plan);
        }

        var rng = new System.Random(runSeed);
        ScatterAcrossFloors(plans, RoomType.Gamble, RollCount(rng, config.DoubleGambleChance), rng);
        ScatterAcrossFloors(plans, RoomType.Secret, RollCount(rng, config.DoubleSecretChance), rng);
        if (excludeGamble)
            foreach (FloorPlan plan in plans) plan.LeafRooms.RemoveAll(type => type == RoomType.Gamble);
        return plans;
    }

    private static int RollCount(System.Random rng, float doubleChance) => rng.NextDouble() < doubleChance ? 2 : 1;

    // 무작위 층에 배정. 같은 층에 여러 개 들어가도 됨.
    private static void ScatterAcrossFloors(List<FloorPlan> plans, RoomType type, int count, System.Random rng)
    {
        for (int i = 0; i < count; i++)
            rng.Pick(plans).LeafRooms.Add(type);
    }
}
