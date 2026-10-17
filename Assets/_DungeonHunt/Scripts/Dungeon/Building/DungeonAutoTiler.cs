using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// 코드 오토타일. 층 전체 바닥 칸(방 + 복도)만 보고 나머지 칸의 종류와 모양을 정해 공용 타일맵에 칠함.
// RuleTile과 달리 방/복도 타일맵 경계, 위/아래/좌우 벽 에셋 구분과 상관없이 이어짐.
//
// 칸 분류 (WallStyle 두께 기준)
//   바닥  : 걸어 다니는 칸 (아래 벽 앞면 칸 제외)
//   정면  : 바로 아래 (위 벽 두께 - 1)칸 안에 바닥 → 바닥 쪽부터 1줄, 2줄...
//   앞면  : 바로 위 (아래 벽 두께 - 1)칸 안에 바닥 (걸어 다니지만 벽이 캐릭터 위에 겹침)
//   천장  : 바닥에서 위 두께 / 아래 두께 / 좌우 두께 안의 나머지 칸
//   틈    : 그 밖
public static class DungeonAutoTiler
{
    public enum Kind : byte { Gap, Floor, Face, Front, Ceiling }

    public sealed class Map
    {
        public RectInt Bounds;
        public Kind[,] Kinds;
        public byte[,] Depth; // 정면/앞면 줄 번호 (1부터)

        public Kind Get(int x, int y)
        {
            x -= Bounds.x; y -= Bounds.y;
            return x < 0 || y < 0 || x >= Bounds.width || y >= Bounds.height ? Kind.Gap : Kinds[x, y];
        }

        public int DepthAt(int x, int y)
        {
            x -= Bounds.x; y -= Bounds.y;
            return x < 0 || y < 0 || x >= Bounds.width || y >= Bounds.height ? 0 : Depth[x, y];
        }
    }

    public static Map Classify(HashSet<Vector2Int> floor, WallStyle style)
    {
        int xMin = int.MaxValue, yMin = int.MaxValue, xMax = int.MinValue, yMax = int.MinValue;
        foreach (Vector2Int c in floor)
        {
            if (c.x < xMin) xMin = c.x;
            if (c.y < yMin) yMin = c.y;
            if (c.x > xMax) xMax = c.x;
            if (c.y > yMax) yMax = c.y;
        }
        int margin = Mathf.Max(style.Top, style.Bottom, style.Side) + 1;
        var map = new Map
        {
            Bounds = floor.Count == 0 ? new RectInt(0, 0, 0, 0) : new RectInt(xMin - margin, yMin - margin, xMax - xMin + 1 + margin * 2, yMax - yMin + 1 + margin * 2),
        };
        map.Kinds = new Kind[map.Bounds.width, map.Bounds.height];
        map.Depth = new byte[map.Bounds.width, map.Bounds.height];

        int faceRows = style.Top - 1, frontRows = style.Bottom - 1;
        for (int ix = 0; ix < map.Bounds.width; ix++)
        {
            for (int iy = 0; iy < map.Bounds.height; iy++)
            {
                int x = map.Bounds.x + ix, y = map.Bounds.y + iy;
                if (floor.Contains(new Vector2Int(x, y))) { map.Kinds[ix, iy] = Kind.Floor; continue; }

                Kind kind = Kind.Gap;
                int depth = 0;
                for (int d = 1; d <= faceRows; d++)
                {
                    if (floor.Contains(new Vector2Int(x, y - d))) { kind = Kind.Face; depth = d; break; }
                }
                if (kind == Kind.Gap)
                {
                    for (int d = 1; d <= frontRows; d++)
                    {
                        if (floor.Contains(new Vector2Int(x, y + d))) { kind = Kind.Front; depth = d; break; }
                    }
                }
                if (kind == Kind.Gap && NearFloor(floor, x, y, style)) kind = Kind.Ceiling;
                map.Kinds[ix, iy] = kind;
                map.Depth[ix, iy] = (byte)depth;
            }
        }
        return map;
    }

    // 이 칸 기준 아래로 위 벽 두께, 위로 아래 벽 두께, 좌우로 좌우 두께 안에 바닥이 있으면 벽(천장)
    private static bool NearFloor(HashSet<Vector2Int> floor, int x, int y, WallStyle style)
    {
        for (int dx = -style.Side; dx <= style.Side; dx++)
            for (int dy = -style.Top; dy <= style.Bottom; dy++)
                if (floor.Contains(new Vector2Int(x + dx, y + dy))) return true;
        return false;
    }

    // ---------------- 칠하기 ----------------

    public static void Paint(Map map, DungeonTileset set, Tilemap floorMap, Tilemap wallMap, Tilemap frontMap, Tilemap gapMap, int seed)
    {
        var floorCells = new List<Vector3Int>(); var floorTiles = new List<TileBase>();
        var wallCells = new List<Vector3Int>(); var wallTiles = new List<TileBase>();
        var frontCells = new List<Vector3Int>(); var frontTiles = new List<TileBase>();
        var gapCells = new List<Vector3Int>(); var gapTiles = new List<TileBase>();

        for (int ix = 0; ix < map.Bounds.width; ix++)
        {
            for (int iy = 0; iy < map.Bounds.height; iy++)
            {
                int x = map.Bounds.x + ix, y = map.Bounds.y + iy;
                var cell = new Vector3Int(x, y, 0);
                switch (map.Kinds[ix, iy])
                {
                    case Kind.Floor:
                        floorCells.Add(cell); floorTiles.Add(FloorTile(map, set, x, y, seed));
                        break;
                    case Kind.Front:
                        // 앞면 칸 밑에도 바닥이 이어짐
                        floorCells.Add(cell); floorTiles.Add(FloorTile(map, set, x, y, seed));
                        frontCells.Add(cell); frontTiles.Add(RowTile(map, set.Front, x, y, Kind.Front, seed));
                        break;
                    case Kind.Face:
                        wallCells.Add(cell); wallTiles.Add(RowTile(map, map.DepthAt(x, y) <= 1 ? set.FaceLow : set.FaceHigh, x, y, Kind.Face, seed));
                        break;
                    case Kind.Ceiling:
                        wallCells.Add(cell); wallTiles.Add(CeilingTile(map, set, x, y));
                        break;
                    default:
                        if (gapMap != null) { gapCells.Add(cell); gapTiles.Add(Pick(set.Gap, x, y, seed)); }
                        break;
                }
            }
        }

        floorMap.ClearAllTiles(); wallMap.ClearAllTiles();
        if (frontMap != null) frontMap.ClearAllTiles();
        if (gapMap != null) gapMap.ClearAllTiles();
        floorMap.SetTiles(floorCells.ToArray(), floorTiles.ToArray());
        wallMap.SetTiles(wallCells.ToArray(), wallTiles.ToArray());
        if (frontMap != null) frontMap.SetTiles(frontCells.ToArray(), frontTiles.ToArray());
        else wallMap.SetTiles(frontCells.ToArray(), frontTiles.ToArray());
        if (gapMap != null) gapMap.SetTiles(gapCells.ToArray(), gapTiles.ToArray());
    }

    // 바닥: 앞면 칸도 바닥으로 봄 (밑으로 바닥이 이어짐)
    private static TileBase FloorTile(Map map, DungeonTileset s, int x, int y, int seed)
    {
        bool F(int dx, int dy) { Kind k = map.Get(x + dx, y + dy); return k == Kind.Floor || k == Kind.Front; }
        bool n = F(0, 1), e = F(1, 0), so = F(0, -1), w = F(-1, 0);
        TileBase center = Pick(s.FloorCenter, x, y, seed);
        if (!n && !w) return Or(s.FloorTopLeft, s.FloorTop, center);
        if (!n && !e) return Or(s.FloorTopRight, s.FloorTop, center);
        if (!n) return Or(s.FloorTop, center);
        if (!so && !w) return Or(s.FloorBottomLeft, s.FloorLeft, center);
        if (!so && !e) return Or(s.FloorBottomRight, s.FloorRight, center);
        if (!so) return Or(s.FloorBottom, center);
        if (!w) return Or(s.FloorLeft, center);
        if (!e) return Or(s.FloorRight, center);
        if (!F(1, 1)) return Or(s.FloorInnerNE, center);
        if (!F(-1, 1)) return Or(s.FloorInnerNW, center);
        if (!F(1, -1)) return Or(s.FloorInnerSE, center);
        if (!F(-1, -1)) return Or(s.FloorInnerSW, center);
        return center;
    }

    // 정면/앞면 줄: 옆 칸이 같은 줄이면 이어짐, 바닥이면 개구부 끝, 그 밖(천장, 다른 줄)이면 벽 끝
    private static TileBase RowTile(Map map, DungeonTileset.FaceRow row, int x, int y, Kind kind, int seed)
    {
        int depth = map.DepthAt(x, y);
        char Side(int dx)
        {
            Kind k = map.Get(x + dx, y);
            if (k == kind && map.DepthAt(x + dx, y) == depth) return 'C';
            return k == Kind.Floor ? 'O' : 'W';
        }
        char l = Side(-1), r = Side(1);
        TileBase middle = Pick(row.Middle, x, y, seed);
        if (l == 'C' && r == 'C') return middle;
        if (l != 'C' && r != 'C')
        {
            if (l == 'W' && r == 'O') return Or(row.SingleWallOpen, row.LeftWall, middle);
            if (l == 'O' && r == 'W') return Or(row.SingleOpenWall, row.RightWall, middle);
            if (l == 'W' && r == 'W') return Or(row.SingleWallWall, row.LeftWall, middle);
            return Or(row.SingleOpenOpen, row.LeftOpen, middle);
        }
        if (l != 'C') return l == 'O' ? Or(row.LeftOpen, row.LeftWall, middle) : Or(row.LeftWall, middle);
        return r == 'O' ? Or(row.RightOpen, row.RightWall, middle) : Or(row.RightWall, middle);
    }

    // 천장 띠(두께 1): 같은 천장끼리 이어진 방향 + 안쪽(바닥/정면/앞면)이 어디인지로 직선/바깥 모서리/안쪽 모서리
    private static TileBase CeilingTile(Map map, DungeonTileset s, int x, int y)
    {
        bool C(int dx, int dy) => map.Get(x + dx, y + dy) == Kind.Ceiling;
        bool In(int dx, int dy) { Kind k = map.Get(x + dx, y + dy); return k == Kind.Floor || k == Kind.Face || k == Kind.Front; }
        bool n = C(0, 1), e = C(1, 0), so = C(0, -1), w = C(-1, 0);
        TileBase fill = Or(s.CeilingFill, s.CeilingTop);

        if (e && w && !n && !so) return In(0, -1) || In(1, -1) || In(-1, -1) ? Or(s.CeilingTop, fill) : Or(s.CeilingBottom, fill);
        if (n && so && !e && !w) return In(1, 0) || In(1, 1) || In(1, -1) ? Or(s.CeilingLeft, fill) : Or(s.CeilingRight, fill);

        // 모서리: 이어진 두 방향 사이 대각선이 안쪽이고 나머지 두 방향이 안쪽이 아니면 바깥(볼록), 아니면 안쪽(오목)
        if (e && so && !n && !w) return Outer(1, -1) ? Or(s.CeilingOuterTL, fill) : Or(s.CeilingInnerTL, fill);
        if (w && so && !n && !e) return Outer(-1, -1) ? Or(s.CeilingOuterTR, fill) : Or(s.CeilingInnerTR, fill);
        if (e && n && !so && !w) return Outer(1, 1) ? Or(s.CeilingOuterBL, fill) : Or(s.CeilingInnerBL, fill);
        if (w && n && !so && !e) return Outer(-1, 1) ? Or(s.CeilingOuterBR, fill) : Or(s.CeilingInnerBR, fill);
        return fill;

        bool Outer(int dx, int dy) => In(dx, dy) && !In(-dx, 0) && !In(0, -dy);
    }

    private static TileBase Pick(TileBase[] tiles, int x, int y, int seed)
    {
        if (tiles == null || tiles.Length == 0) return null;
        if (tiles.Length == 1) return tiles[0];
        unchecked
        {
            int h = x * 73856093 ^ y * 19349663 ^ seed * 83492791;
            h ^= h >> 13;
            return tiles[(h & 0x7fffffff) % tiles.Length];
        }
    }

    private static TileBase Or(TileBase a, TileBase b, TileBase c = null) => a != null ? a : b != null ? b : c;
}
