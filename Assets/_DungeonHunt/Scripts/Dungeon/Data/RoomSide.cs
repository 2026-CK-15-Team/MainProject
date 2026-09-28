using System;
using UnityEngine;

public enum RoomSide
{
    Up,
    Right,
    Down,
    Left
}

public static class RoomSideUtility
{
    public static readonly RoomSide[] All = { RoomSide.Up, RoomSide.Right, RoomSide.Down, RoomSide.Left };

    public static Vector2Int ToOffset(this RoomSide side) => side switch
    {
        RoomSide.Up => Vector2Int.up,
        RoomSide.Right => Vector2Int.right,
        RoomSide.Down => Vector2Int.down,
        RoomSide.Left => Vector2Int.left,
        _ => throw new ArgumentOutOfRangeException(nameof(side))
    };

    public static RoomSide Opposite(this RoomSide side) => (RoomSide)(((int)side + 2) % 4);

    public static RoomSide FromOffset(Vector2Int offset)
    {
        foreach (RoomSide side in All)
        {
            if (side.ToOffset() == offset)
                return side;
        }
        throw new ArgumentException($"맞닿은 칸 아님: {offset}", nameof(offset));
    }
}
