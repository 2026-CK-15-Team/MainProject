using System;
using System.Collections.Generic;
using System.Linq;

// 층 생성 전체 흐름: 방 연결 구조 → 템플릿 배정 → 타일 좌표 배치. 유니티 오브젝트는 안 만듦.
public static class FloorGenerator
{
    // 템플릿 배정용 난수를 연결 구조용 난수랑 분리하려는 값
    private const int TemplateSeedSalt = 0x7E3A11;

    public static FloorBlueprint Generate(FloorPlan plan, IReadOnlyList<RoomTemplateData> templates, int minRoomSpacing, WallStyle wallStyle)
    {
        ThrowIfMissingTypes(plan, templates);

        DungeonLayout layout = DungeonGenerator.Generate(plan, candidate => TemplateSelector.CanAssign(candidate, templates));

        var rng = new System.Random(SeedUtility.Combine(plan.Seed, TemplateSeedSalt));
        int[] templateIndices = TemplateSelector.Assign(layout, templates, rng);
        RoomTemplateData[] assigned = templateIndices.Select(index => templates[index]).ToArray();

        return LayoutSolver.Solve(layout, assigned, templateIndices, minRoomSpacing, wallStyle);
    }

    private static void ThrowIfMissingTypes(FloorPlan plan, IReadOnlyList<RoomTemplateData> templates)
    {
        var required = new List<RoomType> { RoomType.Combat, RoomType.Start, RoomType.Boss };
        if (plan.HasStairs)
            required.Add(RoomType.Stairs);
        required.AddRange(plan.LeafRooms);

        List<RoomType> missing = required.Distinct().Where(type => templates.All(template => template.Type != type)).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException($"{plan.FloorIndex + 1}층: 템플릿 없는 방 종류 - {string.Join(", ", missing)}");
    }
}
