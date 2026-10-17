using System;
using UnityEngine;
using UnityEngine.Tilemaps;

// 층 타일셋 (코드 오토타일용). FloorConfig.Tileset에 넣으면 방/복도 벽을 코드가 칸마다 골라 칠함.
// 칸 종류: 바닥 / 벽 정면(위 벽, 바닥 쪽부터 1줄 2줄...) / 아래 벽 앞면 / 천장(벽 윗면) / 틈.
// 칸이 비어 있으면 같은 분류의 기본 칸으로 대체 → 아트를 일부만 넣어도 깨지지 않음.
// 층끼리 같은 에셋을 써도 됨. 타일 ID는 Docs/ArtResourceList(타일셋) 표와 같음.
[CreateAssetMenu(fileName = "DungeonTileset", menuName = "Dungeon/Dungeon Tileset")]
public class DungeonTileset : ScriptableObject
{
    [Serializable]
    public class FaceRow
    {
        [Tooltip("가운데 (여러 장이면 칸마다 랜덤)")] public TileBase[] Middle = new TileBase[0];
        [Tooltip("_LW 왼쪽 끝, 왼쪽이 벽")] public TileBase LeftWall;
        [Tooltip("_RW 오른쪽 끝, 오른쪽이 벽")] public TileBase RightWall;
        [Tooltip("_LO 왼쪽 끝, 왼쪽이 개구부(바닥)")] public TileBase LeftOpen;
        [Tooltip("_RO 오른쪽 끝, 오른쪽이 개구부(바닥)")] public TileBase RightOpen;
        [Tooltip("_SWO 한 칸짜리, 왼쪽 벽 / 오른쪽 개구부")] public TileBase SingleWallOpen;
        [Tooltip("_SOW 한 칸짜리, 왼쪽 개구부 / 오른쪽 벽")] public TileBase SingleOpenWall;
        [Tooltip("_SWW 한 칸짜리, 양쪽 벽")] public TileBase SingleWallWall;
        [Tooltip("_SOO 한 칸짜리, 양쪽 개구부 (거의 안 나옴)")] public TileBase SingleOpenOpen;
    }

    [Header("바닥 F")]
    [Tooltip("F_C 가운데 (여러 장이면 칸마다 랜덤)")] public TileBase[] FloorCenter = new TileBase[0];
    [Tooltip("F_T 위 테두리 (벽 정면 아래)")] public TileBase FloorTop;
    [Tooltip("F_L 왼쪽 테두리")] public TileBase FloorLeft;
    [Tooltip("F_R 오른쪽 테두리")] public TileBase FloorRight;
    [Tooltip("F_TL 위 왼쪽 바깥 모서리")] public TileBase FloorTopLeft;
    [Tooltip("F_TR 위 오른쪽 바깥 모서리")] public TileBase FloorTopRight;
    [Tooltip("F_INE 안쪽 모서리 (오른쪽 위 대각선이 벽)")] public TileBase FloorInnerNE;
    [Tooltip("F_INW 안쪽 모서리 (왼쪽 위)")] public TileBase FloorInnerNW;
    [Tooltip("F_ISE 안쪽 모서리 (오른쪽 아래)")] public TileBase FloorInnerSE;
    [Tooltip("F_ISW 안쪽 모서리 (왼쪽 아래)")] public TileBase FloorInnerSW;
    [Tooltip("F_B 아래 테두리. 아래 벽 앞면이 덮어서 보통 안 보임 (아래 벽 두께 1이거나 앞면이 반투명일 때만)")] public TileBase FloorBottom;
    [Tooltip("F_BL 아래 왼쪽 바깥 모서리 (위와 같은 경우만)")] public TileBase FloorBottomLeft;
    [Tooltip("F_BR 아래 오른쪽 바깥 모서리 (위와 같은 경우만)")] public TileBase FloorBottomRight;

    [Header("벽 정면 W (위 벽, 충돌)")]
    [Tooltip("W1 바닥에 닿는 줄")] public FaceRow FaceLow = new FaceRow();
    [Tooltip("W2 그 위 줄들 (위 벽 두께 4 이상이면 이 줄이 반복)")] public FaceRow FaceHigh = new FaceRow();

    [Header("아래 벽 앞면 B (캐릭터 위에 그림, 충돌 없음)")]
    public FaceRow Front = new FaceRow();

    [Header("천장 C (벽 윗면, 충돌)")]
    [Tooltip("C_T 위 벽 띠")] public TileBase CeilingTop;
    [Tooltip("C_B 아래 벽 띠")] public TileBase CeilingBottom;
    [Tooltip("C_L 왼쪽 벽")] public TileBase CeilingLeft;
    [Tooltip("C_R 오른쪽 벽")] public TileBase CeilingRight;
    [Tooltip("C_OTL 바깥 모서리 왼쪽 위")] public TileBase CeilingOuterTL;
    [Tooltip("C_OTR 바깥 모서리 오른쪽 위")] public TileBase CeilingOuterTR;
    [Tooltip("C_OBL 바깥 모서리 왼쪽 아래")] public TileBase CeilingOuterBL;
    [Tooltip("C_OBR 바깥 모서리 오른쪽 아래")] public TileBase CeilingOuterBR;
    [Tooltip("C_ITL 안쪽 모서리 왼쪽 위")] public TileBase CeilingInnerTL;
    [Tooltip("C_ITR 안쪽 모서리 오른쪽 위")] public TileBase CeilingInnerTR;
    [Tooltip("C_IBL 안쪽 모서리 왼쪽 아래")] public TileBase CeilingInnerBL;
    [Tooltip("C_IBR 안쪽 모서리 오른쪽 아래")] public TileBase CeilingInnerBR;
    [Tooltip("위 패턴에 안 맞는 천장 (T자 등, 보통 안 나옴)")] public TileBase CeilingFill;

    [Header("틈 G")]
    [Tooltip("룸 사이 빈 공간 (여러 장이면 랜덤). 미방문 룸 가림 덮개도 첫 번째 칸을 씀")] public TileBase[] Gap = new TileBase[0];

    public TileBase FogTile => Gap != null && Gap.Length > 0 ? Gap[0] : null;
}
