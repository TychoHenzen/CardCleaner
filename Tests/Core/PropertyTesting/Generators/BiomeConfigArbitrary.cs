using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using FsCheck;

namespace CardCleaner.Tests.Core.PropertyTesting.Generators;

/// <summary>
///     FsCheck generators for biome configurations used in map generation tests.
/// </summary>
public static class BiomeConfigArbitrary
{
    /// <summary>
    ///     Generator for random biome definitions.
    /// </summary>
    public static Gen<BiomeDefinition> Default =>
        from id in BiomeIdGen
        from signature in CardSignatureArbitrary.Generator
        from passable in TilePoolGen
        from blocked in TilePoolGen
        from blockedPct in Gen.Choose(0, 100).Select(i => i / 100f)
        select new BiomeDefinition(id, signature, passable, blocked, blockedPct);

    /// <summary>
    ///     Generator for biome IDs (string-based).
    /// </summary>
    public static Gen<string> BiomeIdGen =>
        Gen.Elements(
            "plains",
            "forest",
            "desert",
            "mountains",
            "swamp",
            "tundra"
        );

    /// <summary>
    ///     Generator for tile pools with 1-5 entries.
    /// </summary>
    public static Gen<TilePool> TilePoolGen =>
        from entryCount in Gen.Choose(1, 5)
        from entries in Gen.ArrayOf(entryCount, TilePoolEntryGen)
        select CreateTilePool(entries);

    /// <summary>
    ///     Generator for tile pool entries (tile ID + weight).
    /// </summary>
    public static Gen<(string TileId, float Weight)> TilePoolEntryGen =>
        from tileId in Gen.Elements("grass", "dirt", "stone", "water", "sand", "snow", "lava")
        from weight in Gen.Choose(1, 100).Select(i => i / 10f)
        select (tileId, weight);

    private static TilePool CreateTilePool((string TileId, float Weight)[] entries)
    {
        var pool = new TilePool();
        // Deduplicate by tile ID to avoid weird behavior
        var uniqueEntries = entries.GroupBy(e => e.TileId).Select(g => g.First());
        foreach (var (tileId, weight) in uniqueEntries)
            pool.Add(tileId, weight);
        return pool;
    }

    private static IEnumerable<BiomeDefinition> ShrinkBiome(BiomeDefinition biome)
    {
        // Shrink blocked percentage toward zero
        if (biome.BlockedPercentage > 0)
        {
            yield return new BiomeDefinition(
                biome.Id,
                biome.AffinitySignature,
                biome.PassableTiles,
                biome.BlockedTiles,
                0f);
        }

        if (biome.BlockedPercentage > 0.5f)
        {
            yield return new BiomeDefinition(
                biome.Id,
                biome.AffinitySignature,
                biome.PassableTiles,
                biome.BlockedTiles,
                biome.BlockedPercentage / 2f);
        }
    }

    public static Arbitrary<BiomeDefinition> Arbitrary =>
        Arb.From(Default, ShrinkBiome);

    public static void Register() => Arb.Register<BiomeConfigArbitraryProvider>();

    private sealed class BiomeConfigArbitraryProvider
    {
        public static Arbitrary<BiomeDefinition> BiomeDefinition() => Arbitrary;
    }
}

/// <summary>
///     Helper generators for specific biome scenarios.
/// </summary>
public static class BiomeConfigGenerators
{
    /// <summary>
    ///     Generator for biomes of a specific ID.
    /// </summary>
    public static Gen<BiomeDefinition> WithId(string biomeId) =>
        from signature in CardSignatureArbitrary.Generator
        from passable in BiomeConfigArbitrary.TilePoolGen
        from blocked in BiomeConfigArbitrary.TilePoolGen
        from blockedPct in Gen.Choose(0, 100).Select(i => i / 100f)
        select new BiomeDefinition(biomeId, signature, passable, blocked, blockedPct);

    /// <summary>
    ///     Generator for biomes with blocked percentage in a specific range.
    /// </summary>
    public static Gen<BiomeDefinition> WithBlockedPercentage(float min, float max) =>
        from id in BiomeConfigArbitrary.BiomeIdGen
        from signature in CardSignatureArbitrary.Generator
        from passable in BiomeConfigArbitrary.TilePoolGen
        from blocked in BiomeConfigArbitrary.TilePoolGen
        from blockedPct in Gen.Choose((int)(min * 100), (int)(max * 100)).Select(i => i / 100f)
        select new BiomeDefinition(id, signature, passable, blocked, blockedPct);
}
