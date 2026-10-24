using System;
using System.Collections.Generic;
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

// 몬스터 스폰점 하나에서 나올 몬스터 한 마리 (몇 번째 웨이브에 무엇을)
[Serializable]
public class PointSpawn
{
    [Tooltip("등장 웨이브 (1부터). 엘리트룸은 1, 2")]
    [Min(1)] public int Wave = 1;
    public MonsterDefinition Monster;
    [Tooltip("처치 재화 값. 1 이상 (03-1 §8.2)")]
    [Min(1)] public int CurrencyDrop = 1;
}

// 룸 레이아웃 프리셋 안의 고정 지점. 방 프리팹 자식으로 두고 종류를 지정함.
public class RoomPoint : MonoBehaviour
{
    public RoomPointKind Kind;
    [Tooltip("MonsterSpawn 번호 (1부터)")]
    public int Index = 1;
    public RoomObjectRole Role;
    [Tooltip("MonsterSpawn 전용. 이 자리에서 웨이브마다 직접 스폰할 몬스터. " +
             "방 안 스폰점 중 하나라도 채우면 그 방은 층 프리셋 대신 스폰점 설정만으로 웨이브를 만듦")]
    public List<PointSpawn> Spawns = new List<PointSpawn>();

    public bool HasSpawns => Kind == RoomPointKind.MonsterSpawn && Spawns.Exists(s => s != null && s.Monster != null);

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
        if (HasSpawns)
            foreach (PointSpawn s in Spawns)
                if (s != null && s.Monster != null) label += $"\n{s.Wave}W {s.Monster.name}";
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.4f, label);
#endif
    }
}
