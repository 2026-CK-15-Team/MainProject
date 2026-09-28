// Combat 빼고 전부 특수방. 특수방은 문 하나짜리 말단임. (예외: 보스방은 계단방 포함 문 2개)
public enum RoomType
{
    Combat,
    Start,
    Treasure,
    Shop,
    Gamble,
    Secret,
    Boss,
    Stairs
}
