using System.Linq;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using FsCheck;

namespace CardCleaner.Tests.Core.PropertyTesting.Generators;

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
