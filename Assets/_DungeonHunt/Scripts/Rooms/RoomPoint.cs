using UnityEngine;

public enum RoomPointKind
{
    TeleportArrival,  // 순간이동 도착점. 레이아웃마다 정확히 1개 (03 §4)
    RewardSpawn,      // 클리어 보상, 비밀방 보상 지점. 전투룸/엘리트/비밀방에 1개 (03 §8.5)
    RoomObject,       // NPC, 필드 아티팩트, 이동 오브젝트, 상자 지점 (03 §8.5)
    MonsterSpawn,     // 번호 붙은 몬스터 스폰점. 구성 프리셋은 번호만 참조
}

public enum RoomObjectRole
{
    Default,
    NormalChest,   // RunEndTreasureRoom 일반 보물상자
    EndingChest,   // RunEndTreasureRoom 엔딩 상자
}

// 룸 레이아웃 프리셋 안의 고정 지점. 방 프리팹 자식으로 두고 종류를 지정함.
public class RoomPoint : MonoBehaviour
{
    public RoomPointKind Kind;
    [Tooltip("MonsterSpawn 번호 (1부터)")]
    public int Index = 1;
    public RoomObjectRole Role;

    private void OnDrawGizmos()
    {
        Gizmos.color = Kind switch
        {
            RoomPointKind.TeleportArrival => Color.cyan,
            RoomPointKind.RewardSpawn => Color.yellow,
            RoomPointKind.RoomObject => Color.green,
            _ => Color.red,
        };
        Gizmos.DrawWireSphere(transform.position, 0.3f);
        if (Kind == RoomPointKind.MonsterSpawn)
            Gizmos.DrawLine(transform.position + Vector3.left * 0.2f, transform.position + Vector3.right * 0.2f);
#if UNITY_EDITOR
        string label = Kind == RoomPointKind.MonsterSpawn ? $"S{Index}" : Kind == RoomPointKind.RoomObject && Role != RoomObjectRole.Default ? Role.ToString() : Kind.ToString();
        RoomMonsterWaves waves = Kind == RoomPointKind.MonsterSpawn ? GetComponentInParent<RoomMonsterWaves>() : null;
        if (waves != null && waves.HasWaves)
        {
            string spawns = waves.LabelFor(Index);
            if (spawns.Length > 0) label += "\n" + spawns;
        }
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.4f, label);
#endif
    }
}
