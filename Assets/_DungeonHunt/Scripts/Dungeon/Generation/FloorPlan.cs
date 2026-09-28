using System.Collections.Generic;

// 한 층 생성에 필요한 입력 전부. FloorPlan 같으면 같은 층 나옴.
public class FloorPlan
{
    public FloorPlan(int floorIndex, int seed, int combatRoomCount, bool hasLoop, bool hasStairs)
    {
        FloorIndex = floorIndex;
        Seed = seed;
        CombatRoomCount = combatRoomCount;
        HasLoop = hasLoop;
        HasStairs = hasStairs;
    }

    public int FloorIndex { get; }
    public int Seed { get; }
    public int CombatRoomCount { get; }
    public bool HasLoop { get; }
    public bool HasStairs { get; }

    // 시작·보스·계단 빼고 전투방에 붙일 말단 특수방 목록
    public List<RoomType> LeafRooms { get; } = new List<RoomType>();
}
