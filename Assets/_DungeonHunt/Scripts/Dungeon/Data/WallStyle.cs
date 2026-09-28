using System;
using UnityEngine;

public enum WallPart
{
    Top,    // 바닥 위쪽 벽 (벽면 + 천장 닿는 칸)
    Bottom, // 바닥 아래쪽 벽 (천장 닿는 칸만 보임)
    Side    // 좌우 벽
}

// 벽 두께 규칙(칸). 탑다운 3/4 시점이라 위·아래·좌우가 다름. 벽 너머는 빈 공간.
[Serializable]
public class WallStyle
{
    [Tooltip("위쪽 벽 두께. 벽면 그림 (두께-1)칸 + 천장 닿는 칸 1. 전부 충돌")]
    [Min(1)] public int Top = 3;

    [Tooltip("아래쪽 벽 두께. 보이는 건 바깥쪽 천장 닿는 칸 1줄(충돌)뿐, " +
             "안쪽 (두께-1)칸은 바닥 깔리고 캐릭터가 벽에 가려 숨는 칸(충돌 없음)")]
    [Min(1)] public int Bottom = 2;

    [Tooltip("좌우 벽 두께. 전부 충돌")]
    [Min(1)] public int Side = 1;

    // 아래 벽 중 바닥 깔리고 캐릭터 가리는 칸 수
    public int FrontRowCount => Bottom - 1;

    public int ThicknessOf(RoomSide side) => side switch
    {
        RoomSide.Up => Top,
        RoomSide.Down => Bottom,
        _ => Side
    };

    public static WallPart PartOf(RoomSide side) => side switch
    {
        RoomSide.Up => WallPart.Top,
        RoomSide.Down => WallPart.Bottom,
        _ => WallPart.Side
    };
}
