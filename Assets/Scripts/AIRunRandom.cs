public static class AIRunRandom
{
    private static System.Random _random = new System.Random(1337);

    public static int Seed { get; private set; } = 1337;

    public static void BeginRun(int seed)
    {
        Seed = seed;
        _random = new System.Random(seed);
    }

    public static float Value => (float)_random.NextDouble();

    public static int Range(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive) return minInclusive;
        return _random.Next(minInclusive, maxExclusive);
    }

    public static float Range(float minInclusive, float maxInclusive)
    {
        if (maxInclusive <= minInclusive) return minInclusive;
        return minInclusive + (maxInclusive - minInclusive) * Value;
    }
}
