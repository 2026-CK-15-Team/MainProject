using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// 03 §6.4 미방문 룸 가리기. 틈 지형과 같은 흙/돌 덮개를 덮개 전용 타일맵에 칠함.
// - 미방문 룸: 바운딩 전체 (쓰는 문 자리의 입구 칸은 덮지 않음)
// - 복도: 방문한 룸의 쓰는 문 자리에 이어진 것만 보임. 비밀방 복도는 비밀 벽 파괴 전까지 가림
// - 해제: 전투룸 즉시, 비전투 룸 RoomReveal_FadeDuration 페이드
public class FogOfWar : MonoBehaviour
{
    private class FadeJob
    {
        public List<Vector3Int> Cells;
        public float StartTime;
        public float Duration;
    }

    private Tilemap fogMap;
    private Tile fogTile;
    private Color fogColor;
    private readonly Dictionary<RoomRuntime, List<Vector3Int>> roomCells = new Dictionary<RoomRuntime, List<Vector3Int>>();
    private readonly List<(RoomRuntime a, RoomRuntime b, List<Vector3Int> cells, bool secret)> corridors = new List<(RoomRuntime, RoomRuntime, List<Vector3Int>, bool)>();
    private readonly HashSet<int> revealedCorridors = new HashSet<int>();
    private readonly List<FadeJob> fades = new List<FadeJob>();

    public void Build(Grid grid, DungeonBuilder builder, IReadOnlyList<RoomRuntime> rooms, Color color, WallStyle style)
    {
        Clear();
        fogColor = color;
        if (fogMap == null)
        {
            var go = new GameObject("Fog");
            go.transform.SetParent(grid.transform, false);
            fogMap = go.AddComponent<Tilemap>();
            var renderer = go.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = 1000;
        }
        if (fogTile == null)
        {
            fogTile = ScriptableObject.CreateInstance<Tile>();
            fogTile.sprite = PlaceholderArt.Square;
            fogTile.flags = TileFlags.None;
        }
        fogTile.color = Color.white;

        FloorBlueprint blueprint = builder.Blueprint;
        var roomByNode = new Dictionary<int, RoomRuntime>();
        foreach (RoomRuntime room in rooms) roomByNode[room.Node.Id] = room;

        // 룸 덮개 (입구 칸 제외)
        var entranceCells = new HashSet<Vector2Int>();
        foreach (PlacedRoom placed in blueprint.Rooms)
        {
            foreach (RoomSide side in RoomSideUtility.All)
            {
                if (!placed.IsOpen(side)) continue;
                foreach (WallCell cell in placed.GetOpeningSealCells(side, style))
                    entranceCells.Add(placed.Position + cell.Cell);
            }
        }

        var secretParents = new HashSet<(int, int)>();
        foreach (DungeonBuilder.SecretLink link in builder.SecretLinks)
            secretParents.Add((link.Parent.Node.Id, link.Secret.Node.Id));

        foreach (RoomRuntime room in rooms)
        {
            var cells = new List<Vector3Int>();
            RectInt bounds = room.CellBounds;
            foreach (Vector2Int c in bounds.allPositionsWithin)
            {
                bool secretRoom = room.Role == RoomRole.Secret;
                if (!secretRoom && entranceCells.Contains(c)) continue;
                cells.Add((Vector3Int)c);
            }
            roomCells[room] = cells;
            Paint(cells, 1f);
        }

        // 복도 덮개: 복도 바닥 + 둘레 벽
        var wallCells = new List<Vector2Int>();
        foreach (WallCell wall in blueprint.CorridorWalls) wallCells.Add(wall.Cell);
        foreach (Corridor corridor in blueprint.Corridors)
        {
            RoomRuntime a = roomByNode[corridor.Link.A.Id];
            RoomRuntime b = roomByNode[corridor.Link.B.Id];
            bool secret = a.Role == RoomRole.Secret || b.Role == RoomRole.Secret;
            var floorSet = new HashSet<Vector2Int>(corridor.Cells);
            var cells = new List<Vector3Int>();
            foreach (Vector2Int c in corridor.Cells) cells.Add((Vector3Int)c);
            foreach (Vector2Int w in wallCells)
            {
                if (NearAny(w, floorSet, 3)) cells.Add((Vector3Int)w);
            }
            corridors.Add((a, b, cells, secret));
            Paint(cells, 1f);
        }
    }

    private static bool NearAny(Vector2Int cell, HashSet<Vector2Int> set, int range)
    {
        for (int dx = -range; dx <= range; dx++)
            for (int dy = -range; dy <= range; dy++)
                if (set.Contains(cell + new Vector2Int(dx, dy))) return true;
        return false;
    }

    public void Clear()
    {
        if (fogMap != null) fogMap.ClearAllTiles();
        roomCells.Clear();
        corridors.Clear();
        revealedCorridors.Clear();
        fades.Clear();
    }

    public void RevealRoom(RoomRuntime room, bool instant)
    {
        if (!roomCells.TryGetValue(room, out List<Vector3Int> cells)) return;
        roomCells.Remove(room);
        if (instant) Paint(cells, 0f);
        else fades.Add(new FadeJob { Cells = cells, StartTime = Time.unscaledTime, Duration = GameBalance.Current.CombatRoom.RoomRevealFadeDuration });
        RefreshCorridors();
    }

    // 방문 룸의 쓰는 문 자리에 이어진 복도만 보임
    public void RefreshCorridors()
    {
        for (int i = 0; i < corridors.Count; i++)
        {
            if (revealedCorridors.Contains(i)) continue;
            var c = corridors[i];
            bool visible = c.secret ? IsSecretOpen(c.a, c.b) : (c.a.Visited || c.b.Visited);
            if (!visible) continue;
            revealedCorridors.Add(i);
            Paint(c.cells, 0f);
        }
    }

    private readonly HashSet<RoomRuntime> openedSecrets = new HashSet<RoomRuntime>();

    public void OnSecretOpened(RoomRuntime secretRoom)
    {
        openedSecrets.Add(secretRoom);
        RefreshCorridors();
    }

    private bool IsSecretOpen(RoomRuntime a, RoomRuntime b)
        => openedSecrets.Contains(a.Role == RoomRole.Secret ? a : b);

    private void Paint(List<Vector3Int> cells, float alpha)
    {
        if (fogMap == null) return;
        Color color = new Color(fogColor.r, fogColor.g, fogColor.b, alpha);
        foreach (Vector3Int cell in cells)
        {
            if (alpha <= 0f)
            {
                fogMap.SetTile(cell, null);
                continue;
            }
            fogMap.SetTile(cell, fogTile);
            fogMap.SetTileFlags(cell, TileFlags.None);
            fogMap.SetColor(cell, color);
        }
    }

    private void Update()
    {
        for (int i = fades.Count - 1; i >= 0; i--)
        {
            FadeJob job = fades[i];
            float t = Mathf.Clamp01((Time.unscaledTime - job.StartTime) / Mathf.Max(0.01f, job.Duration));
            Paint(job.Cells, 1f - t);
            if (t >= 1f) fades.RemoveAt(i);
        }
    }
}
