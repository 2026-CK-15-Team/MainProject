using UnityEngine;

// 게임 전체 설정 루트. 씬의 GameBootstrap이 참조함.
[CreateAssetMenu(fileName = "GameConfig", menuName = "DungeonHunt/Game Config")]
public class GameConfig : ScriptableObject
{
    [Tooltip("던전 생성 설정 (층 구조, 방 프리팹)")]
    public RunConfig Run;
    [Tooltip("튜닝 파라미터 (PrototypeDefault)")]
    public GameBalance Balance;
    [Tooltip("층별 콘텐츠. RunConfig.Floors와 같은 순서 (1층부터)")]
    public FloorContent[] Floors;
    [Tooltip("활성 아티팩트 카탈로그")]
    public ArtifactCatalog Artifacts;
    [Tooltip("무기 3종 정의 (비우면 플레이어 WeaponController 설정 사용)")]
    public WeaponScriptable[] Weapons;
    [Tooltip("UI 폰트. 비우면 OS 한글 폰트")]
    public Font UIFont;
    [Tooltip("플레이어 프리팹. 비우면 씬의 PlayerController 또는 기본 도형 플레이어")]
    public GameObject PlayerPrefab;

    public FloorContent GetFloor(int index)
    {
        if (Floors == null || Floors.Length == 0) return null;
        return Floors[Mathf.Clamp(index, 0, Floors.Length - 1)];
    }

    public int FloorCount => Run != null && Run.Floors != null ? Run.Floors.Length : 0;
}
