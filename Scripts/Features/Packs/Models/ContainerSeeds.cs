namespace CardCleaner.Scripts.Features.Packs.Models;

/// <summary>Derives the seed of each item a sealed container opens into.</summary>
internal static class ContainerSeeds
{
    // Each child of a sealed container gets its own seed, derived from the parent's and the child's index. The
    // parent seed is mixed before the index is added, so neighbouring parent seeds (777, 778) share no child seed.
    internal static ulong ChildSeed(ulong parentSeed, int index) =>
        SplitMix64(unchecked(SplitMix64(parentSeed) + (ulong)index));

    // One splitmix64 step: spreads a counter into a well-mixed seed.
    private static ulong SplitMix64(ulong value)
    {
        unchecked
        {
            var z = value + 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}
