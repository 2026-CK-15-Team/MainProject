using System;
using System.Collections.Generic;
using System.Linq;

// 방마다 종류 같고, 이어진 방향마다 문 있는 템플릿 배정함.
public static class TemplateSelector
{
    public static bool CanAssign(DungeonLayout layout, IReadOnlyList<RoomTemplateData> templates)
        => layout.Rooms.All(room => GetCandidates(room, templates).Count > 0);

    // 결과는 방 Id 순서대로 템플릿 인덱스
    public static int[] Assign(DungeonLayout layout, IReadOnlyList<RoomTemplateData> templates, System.Random rng)
    {
        var result = new int[layout.Rooms.Count];
        foreach (RoomNode room in layout.Rooms)
        {
            List<int> candidates = GetCandidates(room, templates);
            if (candidates.Count == 0)
                throw new InvalidOperationException($"{room.Type} 방에 맞는 템플릿 없음. 필요한 문: {string.Join(", ", GetRequiredSides(room))}");

            result[room.Id] = rng.Pick(candidates);
        }
        return result;
    }

    private static List<int> GetCandidates(RoomNode room, IReadOnlyList<RoomTemplateData> templates)
    {
        var candidates = new List<int>();
        for (int i = 0; i < templates.Count; i++)
        {
            RoomTemplateData template = templates[i];
            if (template.Type == room.Type && GetRequiredSides(room).All(template.HasDoorway))
                candidates.Add(i);
        }
        return candidates;
    }

    private static IEnumerable<RoomSide> GetRequiredSides(RoomNode room)
        => room.Neighbors.Select(room.SideTowards);
}
