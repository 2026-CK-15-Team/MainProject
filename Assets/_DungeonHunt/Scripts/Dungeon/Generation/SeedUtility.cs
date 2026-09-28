public static class SeedUtility
{
    // 두 값 섞어서 새 시드 만듦. HashCode.Combine은 실행마다 값이 달라져서 재현성 깨짐 → 안 씀.
    public static int Combine(int seed, int salt)
    {
        unchecked
        {
            uint hash = (uint)seed * 0x9E3779B1u ^ (uint)salt * 0x85EBCA77u;
            hash ^= hash >> 15;
            hash *= 0x2C1B3C6Du;
            hash ^= hash >> 12;
            hash *= 0x297A2D39u;
            hash ^= hash >> 15;
            return (int)hash;
        }
    }
}
