using CardCleaner.Scripts.Core.Interfaces;

namespace CardCleaner.Scripts.Core.Randomness;

public static class RandomSources
{
    public static IRandom Create(ulong seed) => new Pcg32Random(seed);

    public static IRandom Create() => new Pcg32Random();
}
