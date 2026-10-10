using System.Collections.Generic;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.Support;

/// <summary>
///     Shared builders for WFC tests that run on a synthetic four-tile (A, B, C, D) biome.
/// </summary>
public static class WfcTestFixtures
{
    public const string TestBiomeId = "test";

    /// <summary>
    ///     Every pair of A-D tiles may be adjacent.
    /// </summary>
    public static WfcAdjacencyRules FullAdjacencyRules()
    {
        return new WfcAdjacencyRules(new[]
        {
            ("A", "B"),
            ("B", "C"),
            ("A", "C"),
            ("C", "D"),
            ("A", "D"),
            ("B", "D")
        });
    }

    /// <summary>
    ///     Strict chain: only A-B, B-C and C-D may be adjacent.
    /// </summary>
    public static WfcAdjacencyRules StrictChainRules()
    {
        return new WfcAdjacencyRules(new[]
        {
            ("A", "B"),
            ("B", "C"),
            ("C", "D")
        });
    }

    /// <summary>
    ///     A biome whose passable pool contains A, B, C and D with equal weight.
    /// </summary>
    public static BiomeDefinition CreateAbcdBiome()
    {
        var passable = new TilePool();
        passable.Add("A", 1.0f);
        passable.Add("B", 1.0f);
        passable.Add("C", 1.0f);
        passable.Add("D", 1.0f);

        return new BiomeDefinition(
            TestBiomeId,
            new CardSignature(),
            passable,
            new TilePool(),
            0.0f);
    }

    /// <summary>
    ///     Creates a TileRegistry with the specified tiles allowed in the given biome.
    /// </summary>
    public static TileRegistry CreateTestTileRegistry(string biomeId, params string[] tileIds)
    {
        var registry = new TileRegistry();
        registry.Clear(); // Clear production tiles loaded by constructor
        foreach (var tileId in tileIds)
        {
            registry.RegisterTile(new TileDefinition(
                tileId,
                tileId,
                TilePassability.Passable,
                Vector2I.Zero,
                new TileDefinitionOptions
                {
                    AllowedBiomes = new HashSet<string> { biomeId }
                }));
        }

        return registry;
    }

    /// <summary>
    ///     A catalog over the production TileRegistry, the pairing WfcGridFingerprintTest pins with the
    ///     production transition pairs.
    /// </summary>
    public static TileRegistryWfcCatalog ProductionCatalog() => new(new TileRegistry());

    /// <summary>
    ///     Asserts that a generation succeeded, quoting the generator's error message when it did not.
    /// </summary>
    public static void AssertSucceeded(WfcGenerationResult result, string label = "Generation")
    {
        AssertThat(result.Success)
            .OverrideFailureMessage($"{label} failed: {result.ErrorMessage}")
            .IsTrue();
    }
}
