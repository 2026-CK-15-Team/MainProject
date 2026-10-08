using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// 03 §6.4 미방문 룸 가리기. 틈 지형과 같은 흙/돌 덮개를 덮개 전용 타일맵에 칠함.
// - 미방문 룸: 바운딩 전체 (쓰는 문 자리의 입구 칸은 덮지 않음)
// - 복도: 방문한 룸의 쓰는 문 자리에 이어진 것만 보임. 비밀방 복도는 비밀 벽 파괴 전까지 가림
// - 해제: 전투룸 즉시, 비전투 룸 RoomReveal_FadeDuration 페이드
// - 문 틈 표시: 보이는 복도가 미방문 룸에 닿는 곳에 그 룸 바닥 경계 한 칸만 어둡게 (비밀방 쪽은 없음)
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
    // (미방문 룸, 그 문으로 이어진 이웃 룸, 바닥 경계 칸)
    private readonly List<(RoomRuntime room, RoomRuntime neighbor, List<Vector3Int> cells)> doorGaps = new List<(RoomRuntime, RoomRuntime, List<Vector3Int>)>();
    private readonly HashSet<int> shownGaps = new HashSet<int>();
    // 문 틈은 반투명 어둠: 진입선 직전까지 플레이어가 덮개 밑으로 사라지지 않음
    private const float DoorGapAlpha = 0.6f;

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

        // 문 틈: 이웃 룸 쪽 문 자리의 입구 칸에 맞닿은 바닥 칸 (비밀방과 그 부모 쪽 문 자리는 제외)
        foreach (RoomRuntime room in rooms)
        {
            if (room.Role == RoomRole.Secret) continue;
            PlacedRoom placed = blueprint.Rooms[room.Node.Id];
            var floorCells = new HashSet<Vector2Int>(room.FloorCells);
            foreach (RoomNode neighborNode in room.Node.Neighbors)
            {
                RoomRuntime neighbor = roomByNode[neighborNode.Id];
                if (neighbor.Role == RoomRole.Secret) continue;
                RoomSide side = room.Node.SideTowards(neighborNode);
                var gap = new List<Vector3Int>();
                foreach (WallCell wall in placed.GetOpeningSealCells(side, style))
                {
                    Vector2Int e = placed.Position + wall.Cell;
                    foreach (Vector2Int d in Neighbors4)
                    {
                        Vector2Int c = e + d;
                        if (floorCells.Contains(c) && !gap.Contains((Vector3Int)c)) gap.Add((Vector3Int)c);
                    }
                }
                if (gap.Count > 0) doorGaps.Add((room, neighbor, gap));
            }
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

    private static readonly Vector2Int[] Neighbors4 = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

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
        doorGaps.Clear();
        shownGaps.Clear();
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

        // 문 틈: 이웃(방문)의 쓰는 문 자리 복도가 보이면 미방문 룸 바닥 경계 한 칸을 어둡게
        for (int i = 0; i < doorGaps.Count; i++)
        {
            if (shownGaps.Contains(i)) continue;
            var g = doorGaps[i];
            if (g.room.Visited || !g.neighbor.Visited) continue;
            shownGaps.Add(i);
            PaintColor(g.cells, new Color(0f, 0f, 0f, DoorGapAlpha));
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

    private void PaintColor(List<Vector3Int> cells, Color color)
    {
        if (fogMap == null) return;
        foreach (Vector3Int cell in cells)
        {
            fogMap.SetTile(cell, fogTile);
            fogMap.SetTileFlags(cell, TileFlags.None);
            fogMap.SetColor(cell, color);
        }
    }

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
