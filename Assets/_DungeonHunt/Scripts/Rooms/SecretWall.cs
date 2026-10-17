using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// 부모 룸에서 비밀방 복도로 이어지는 문 자리를 막는 파괴 가능한 벽 (03-5 §4).
// 그림은 일반 벽 타일과 같고(벽 타일맵에 칠함), 이 컴포넌트는 그 칸 목록만 기억함.
// 플레이어 공격 1회 명중이면 무기/피해 무관하게 파괴. 잠긴 전투룸의 비밀 벽은 클리어 전 파괴 불가.
public class SecretWall : MonoBehaviour
{
    private static readonly List<SecretWall> all = new List<SecretWall>();

    private RoomRuntime parentRoom;
    private RoomRuntime secretRoom;
    private Tilemap floorMap, wallMap, frontMap;
    private readonly List<(Vector3Int cell, bool isFront)> cells = new List<(Vector3Int, bool)>();
    private TileBase floorTile;
    private RoomDoor door;
    private Rect worldRect;
    private Vector2Int cellOrigin;
    public bool IsBroken { get; private set; }

    public static SecretWall Create(RoomRuntime parent, RoomRuntime secret, Room parentRoomPrefab, List<WallCell> sealedCells, TileBase floorTile, RoomDoor door)
    {
        var go = new GameObject($"SecretWall_{secret.name}");
        go.transform.SetParent(parent.transform, false);
        var wall = go.AddComponent<SecretWall>();
        wall.parentRoom = parent;
        wall.secretRoom = secret;
        wall.floorMap = parentRoomPrefab.FloorTilemap;
        wall.wallMap = parentRoomPrefab.WallTilemap;
        wall.frontMap = parentRoomPrefab.WallFrontTilemap;
        wall.floorTile = floorTile;
        wall.door = door;
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
        Vector2 origin = parentRoomPrefab.transform.position;
        wall.cellOrigin = Vector2Int.RoundToInt(origin);
        foreach (WallCell c in sealedCells)
        {
            wall.cells.Add(((Vector3Int)c.Cell, c.IsFront));
            Vector2 world = origin + c.Cell;
            min = Vector2.Min(min, world);
            max = Vector2.Max(max, world + Vector2.one);
        }
        wall.worldRect = new Rect(min, max - min);
        all.Add(wall);
        return wall;
    }

    private void OnDestroy() => all.Remove(this);

    // 플레이어 공격이 벽에 맞은 지점으로 비밀 벽 판정
    public static void TryBreakAt(Vector2 hitPoint, Vector2 direction, Collider2D collider)
    {
        Vector2 probe = hitPoint + direction.normalized * 0.05f;
        foreach (SecretWall wall in all.ToArray())
        {
            if (wall == null || wall.IsBroken) continue;
            Rect r = wall.worldRect;
            r.xMin -= 0.05f; r.yMin -= 0.05f; r.xMax += 0.05f; r.yMax += 0.05f;
            if (r.Contains(probe) || r.Contains(hitPoint))
            {
                wall.TryBreak();
                return;
            }
        }
    }

    public bool TryBreak()
    {
        if (IsBroken) return false;
        // 잠긴 전투룸 안의 비밀 벽은 클리어 전 파괴 불가 (03-5 §4 5번)
        if (parentRoom != null && parentRoom.IsLocked) return false;
        IsBroken = true;

        // 쓰는 문 자리의 열린 입구와 같은 모습으로
        foreach ((Vector3Int cell, bool isFront) in cells)
        {
            if (isFront)
            {
                if (frontMap != null) frontMap.SetTile(cell, null);
            }
            else
            {
                wallMap.SetTile(cell, null);
                if (floorMap != null && floorTile != null) floorMap.SetTile(cell, floorTile);
            }
        }
        if (door != null)
        {
            door.gameObject.SetActive(true);
            door.SetLocked(false);
        }
        // 코드 오토타일이면 열린 칸 주변 벽 모양을 다시 고름
        var opened = new List<Vector2Int>();
        foreach ((Vector3Int cell, bool _) in cells) opened.Add(cellOrigin + (Vector2Int)cell);
        GameServices.Floor?.Builder?.OpenCells(opened);
        GameServices.Floor?.OnSecretWallBroken(this, secretRoom);
        (secretRoom != null ? secretRoom.Content as SecretRoomController : null)?.Reveal();
        GameServices.Feedback?.SpawnRewardBurst(worldRect.center, new Color(0.8f, 0.7f, 0.5f));
        return true;
    }

    public RoomRuntime SecretRoom => secretRoom;
    public RoomRuntime ParentRoom => parentRoom;
}
