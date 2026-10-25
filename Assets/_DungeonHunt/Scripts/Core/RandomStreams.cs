// 런 시드에서 용도별 난수를 파생함 (03 §6.1 런 시드와 파생 시드).
// 같은 런 시드 → 같은 콘텐츠 계획. 전투에서 난수를 얼마나 썼든 다음 층 맵과 콘텐츠는 안 바뀜.
public static class RandomStreams
{
    public const int ContentSalt = 0x0C0FFEE;
    public const int CombatSalt = 0x0BA771E;
    public const int LootSalt = 0x0A57F00;
    public const int BossSalt = 0x0B055;

    public static int RunSeed { get; private set; }

    // 전투 난수 (몬스터 첫 공격 분산, 단발/연발 추첨, 스트레이프, 치명타)
    public static System.Random Combat { get; private set; } = new System.Random(1);

    // 보상 추첨 (보상 풀, 아티팩트 ID, 상점 재고, 도박)
    public static System.Random Loot { get; private set; } = new System.Random(2);

    // 보스 패턴 난수 (05 §8.5). 전투 난수와 따로, 조우 시작 때 초기화 → 같은 런 시드면 같은 패턴 순서
    public static System.Random Boss { get; private set; } = new System.Random(3);

    public static void BeginBoss() => Boss = new System.Random(SeedUtility.Combine(RunSeed, BossSalt));

    public static void Begin(int runSeed)
    {
        RunSeed = runSeed;
        Combat = new System.Random(SeedUtility.Combine(runSeed, CombatSalt));
        Loot = new System.Random(SeedUtility.Combine(runSeed, LootSalt));
    }

    public static System.Random CreateContentRandom(int salt)
        => new System.Random(SeedUtility.Combine(SeedUtility.Combine(RunSeed, ContentSalt), salt));

    public static float Range(this System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);
    public static bool Chance(this System.Random rng, float probability) => rng.NextDouble() < probability;
}
