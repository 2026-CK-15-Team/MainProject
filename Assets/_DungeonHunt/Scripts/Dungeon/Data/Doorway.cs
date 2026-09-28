using System;
using UnityEngine;

// 방 바운딩 경계 위에 있는 문(뚫린 구간) 하나. 좌표는 방 기준(왼쪽 아래가 0,0).
[Serializable]
public struct Doorway
{
    public RoomSide Side;

    [Tooltip("변 따라 문이 시작되는 칸. Up/Down은 x, Left/Right는 y")]
    public int Start;

    public int Width;

    public Doorway(RoomSide side, int start, int width)
    {
        Side = side;
        Start = start;
        Width = width;
    }

    // 문의 index번째 칸
    public Vector2Int GetCell(Vector2Int roomSize, int index)
    {
        int along = Start + index;
        return Side switch
        {
            RoomSide.Up => new Vector2Int(along, roomSize.y - 1),
            RoomSide.Down => new Vector2Int(along, 0),
            RoomSide.Left => new Vector2Int(0, along),
            RoomSide.Right => new Vector2Int(roomSize.x - 1, along),
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}
