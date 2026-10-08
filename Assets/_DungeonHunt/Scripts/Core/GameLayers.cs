using UnityEngine;

// 레이어, 태그 이름과 마스크. 기존 무기 코드 규칙(Tag "Wall", Layer "Wall") 그대로 씀.
public static class GameLayers
{
    public const string WallTag = "Wall";
    public const string WallLayerName = "Wall";
    public const string EnemyLayerName = "Enemy";
    public const string PlayerLayerName = "Player";
    public const string PlayerProjectileLayerName = "PlayerProjectile";

    public static int Wall => LayerMask.NameToLayer(WallLayerName);
    public static int WallMask => LayerMask.GetMask(WallLayerName);

    // 벽 레이어가 없는 프로젝트에서도 동작하도록 기본 레이어까지 포함하지 않음. 레이어 설정은 에디터 작업 목록 참고.
    public static bool IsWall(Collider2D collider)
        => collider != null && (collider.gameObject.layer == Wall || collider.CompareTag(WallTag));
}

// 1H = 캐릭터 키 = 타일 1칸 = 1유닛 (03 §6.1.4, 02 §3.3)
public static class GameUnits
{
    public const float H = 1f;
}
