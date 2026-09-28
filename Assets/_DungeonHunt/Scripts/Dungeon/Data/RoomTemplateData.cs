using System;
using System.Collections.Generic;
using UnityEngine;

// 생성기가 방 프리팹에서 필요한 정보. 크기랑 문은 Bake로 채움.
[Serializable]
public class RoomTemplateData
{
    [SerializeField] private RoomType type;
    [SerializeField] private Vector2Int size;
    [SerializeField] private List<Doorway> doorways = new List<Doorway>();

    public RoomTemplateData()
    {
    }

    public RoomTemplateData(RoomType type, Vector2Int size, List<Doorway> doorways)
    {
        this.type = type;
        this.size = size;
        this.doorways = doorways;
    }

    public RoomType Type => type;
    public Vector2Int Size => size;
    public IReadOnlyList<Doorway> Doorways => doorways;

    public bool HasDoorway(RoomSide side) => doorways.Exists(doorway => doorway.Side == side);

    public Doorway GetDoorway(RoomSide side)
    {
        foreach (Doorway doorway in doorways)
        {
            if (doorway.Side == side)
                return doorway;
        }
        throw new InvalidOperationException($"{type} 템플릿의 {side} 방향에 문 없음");
    }

    public void SetShape(Vector2Int size, List<Doorway> doorways)
    {
        this.size = size;
        this.doorways = doorways;
    }
}
