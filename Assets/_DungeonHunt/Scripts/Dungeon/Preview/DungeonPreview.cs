using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

// 에디터 확인용. 시드랑 층 바꾸면 씬 뷰에 생성 결과를 기즈모로 그려줌.
// 층에 방 프리팹 없으면 임시 모양 방으로 보여줌.
public class DungeonPreview : MonoBehaviour
{
    private enum View
    {
        Graph,
        Placement
    }

    [SerializeField] private RunConfig runConfig;
    [SerializeField] private int runSeed;
    [SerializeField, Min(0)] private int floorIndex;
    [SerializeField] private View view = View.Placement;

    [Tooltip("Graph 보기에서 격자 한 칸 크기")]
    [SerializeField, Min(0.5f)] private float graphCellSpacing = 2f;

    [NonSerialized] private FloorBlueprint blueprint;
    [NonSerialized] private bool dirty = true;

    private void OnValidate() => dirty = true;

    [ContextMenu("Randomize Seed")]
    private void RandomizeSeed()
    {
        runSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        dirty = true;
    }

    // 시드 여러 개 돌려서 규칙 위반, 복도 겹침, 재현성(같은 시드 두 번 뽑아서 똑같은지) 확인함.
    [ContextMenu("Validate 1000 Seeds")]
    private void ValidateManySeeds()
    {
        if (runConfig == null) return;

        const int seedCount = 1000;
        int failures = 0;

        for (int seed = 0; seed < seedCount; seed++)
        {
            List<FloorPlan> plans = RunPlanner.Plan(runConfig, seed);
            List<FloorPlan> replayPlans = RunPlanner.Plan(runConfig, seed);

            for (int floor = 0; floor < plans.Count; floor++)
            {
                FloorBlueprint generated = Generate(plans[floor]);
                var errors = new List<string>();
                errors.AddRange(LayoutValidator.Validate(generated.Layout));
                errors.AddRange(LayoutValidator.ValidateBlueprint(generated));

                if (Describe(generated) != Describe(Generate(replayPlans[floor])))
                    errors.Add("같은 시드인데 결과 다름");

                if (errors.Count == 0) continue;

                failures++;
                Debug.LogWarning($"시드 {seed}, {floor + 1}층: {string.Join(" / ", errors.Take(3))}");
            }
        }

        Debug.Log($"시드 {seedCount}개 검사 끝. 실패 {failures}건");
    }

    private void OnDrawGizmos()
    {
        if (dirty)
        {
            dirty = false;
            Regenerate();
        }
        if (blueprint == null) return;

        if (view == View.Graph)
            DrawGraph();
        else
            DrawPlacement();
    }

    private void Regenerate()
    {
        blueprint = null;
        if (runConfig == null || runConfig.Floors == null || runConfig.Floors.Length == 0) return;

        List<FloorPlan> plans = RunPlanner.Plan(runConfig, runSeed);
        FloorPlan plan = plans[Mathf.Clamp(floorIndex, 0, plans.Count - 1)];

        try
        {
            blueprint = Generate(plan);
        }
        catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
        {
            Debug.LogError(e.Message, this);
            return;
        }

        foreach (string error in LayoutValidator.Validate(blueprint.Layout).Concat(LayoutValidator.ValidateBlueprint(blueprint)))
            Debug.LogWarning(error, this);
    }

    private FloorBlueprint Generate(FloorPlan plan)
    {
        FloorConfig floor = runConfig.Floors[plan.FloorIndex];
        List<RoomTemplateData> templates = floor.RoomPrefabs != null && floor.RoomPrefabs.Length > 0
            ? floor.RoomPrefabs.Select(prefab => prefab.Template).ToList()
            : CreatePlaceholderTemplates(plan.Seed);
        return FloorGenerator.Generate(plan, templates, floor.MinRoomSpacing);
    }

    private void DrawGraph()
    {
        Vector3 ToWorld(Vector2Int cell) => transform.position + new Vector3(cell.x, cell.y) * graphCellSpacing;

        Gizmos.color = Color.white;
        foreach (RoomLink link in blueprint.Layout.Links)
            Gizmos.DrawLine(ToWorld(link.A.Cell), ToWorld(link.B.Cell));

        foreach (RoomNode room in blueprint.Layout.Rooms)
        {
            Gizmos.color = ColorOf(room.Type);
            Gizmos.DrawCube(ToWorld(room.Cell), Vector3.one * graphCellSpacing * 0.5f);
            DrawLabel(ToWorld(room.Cell), room.Type.ToString());
        }
    }

    // 1칸 = 1유닛 기준
    private void DrawPlacement()
    {
        Vector3 ToWorld(Vector2Int cell) => transform.position + new Vector3(cell.x + 0.5f, cell.y + 0.5f);

        Gizmos.color = new Color(0.8f, 0.8f, 0.8f, 0.6f);
        foreach (Vector2Int cell in blueprint.CorridorFloor)
            Gizmos.DrawCube(ToWorld(cell), Vector3.one);

        Gizmos.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        foreach (Vector2Int cell in blueprint.CorridorWalls)
            Gizmos.DrawCube(ToWorld(cell), Vector3.one);

        foreach (PlacedRoom room in blueprint.Rooms)
        {
            RectInt bounds = room.Bounds;
            Vector3 center = transform.position + new Vector3(bounds.center.x, bounds.center.y);

            Gizmos.color = ColorOf(room.Node.Type);
            Gizmos.DrawWireCube(center, new Vector3(bounds.width, bounds.height));
            DrawLabel(center, room.Node.Type.ToString());

            Gizmos.color = Color.red;
            foreach (Vector2Int cell in room.GetSealedCells())
                Gizmos.DrawCube(ToWorld(room.Position + cell), Vector3.one * 0.8f);
        }
    }

    private static void DrawLabel(Vector3 position, string text)
    {
#if UNITY_EDITOR
        UnityEditor.Handles.Label(position, text);
#endif
    }

    // 방 프리팹 없을 때 쓰는 임시 모양. 전부 4면에 폭 3짜리 문 있음.
    private static List<RoomTemplateData> CreatePlaceholderTemplates(int seed)
    {
        const int doorWidth = 3;
        const int variantsPerType = 3;
        var rng = new System.Random(seed);
        var templates = new List<RoomTemplateData>();

        foreach (RoomType type in Enum.GetValues(typeof(RoomType)))
        {
            for (int i = 0; i < variantsPerType; i++)
            {
                var size = new Vector2Int(rng.Next(12, 25), rng.Next(10, 21));
                var doorways = new List<Doorway>();
                foreach (RoomSide side in RoomSideUtility.All)
                {
                    int length = side == RoomSide.Up || side == RoomSide.Down ? size.x : size.y;
                    doorways.Add(new Doorway(side, rng.Next(1, length - doorWidth), doorWidth));
                }
                templates.Add(new RoomTemplateData(type, size, doorways));
            }
        }
        return templates;
    }

    private static string Describe(FloorBlueprint target)
    {
        var builder = new StringBuilder();
        foreach (PlacedRoom room in target.Rooms)
            builder.Append($"{room.Node.Type}{room.Node.Cell}{room.TemplateIndex}{room.Position};");
        foreach (RoomLink link in target.Layout.Links)
            builder.Append($"{link.A.Id}-{link.B.Id};");
        builder.Append(target.CorridorFloor.Count);
        return builder.ToString();
    }

    private static Color ColorOf(RoomType type) => type switch
    {
        RoomType.Combat => Color.gray,
        RoomType.Start => Color.green,
        RoomType.Treasure => Color.yellow,
        RoomType.Shop => Color.cyan,
        RoomType.Gamble => new Color(1f, 0.5f, 0f),
        RoomType.Secret => new Color(0.5f, 0f, 0.8f),
        RoomType.Boss => Color.red,
        RoomType.Stairs => Color.magenta,
        _ => Color.white
    };
}
