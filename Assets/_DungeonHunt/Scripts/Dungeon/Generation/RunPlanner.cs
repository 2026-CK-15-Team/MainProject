using System.Collections.Generic;

// 런 시드로 층별 계획 정함. 여러 층에 걸친 규칙(도박방·비밀방 개수)은 여기서 처리.
public static class RunPlanner
{
    public static List<FloorPlan> Plan(RunConfig config, int runSeed)
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
