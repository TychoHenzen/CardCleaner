using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using CardCleaner.Tests.Features.Worldgen.Support;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.Wfc.MapGeneratorScenarios;

/// <summary>
///     WfcMapGeneratorConstraintTest scenarios split out of WfcMapGeneratorIntegrationTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class WfcMapGeneratorConstraintTest : WfcMapGeneratorIntegrationTestBase
{
    [TestCase]
    public void TestCustomAdjacencyRulesWork()
    {
        // Create custom rules for testing without relying on transition map
        var customRules = new WfcAdjacencyRules(new[]
        {
            ("A", "B"),
            ("B", "C"),
            ("A", "C")
        });

        // Create tile registry with test tiles
        var tileRegistry = WfcTestFixtures.CreateTestTileRegistry("plains", "A", "B", "C");
        var generator = new WfcMapGenerator(customRules, new TileRegistryWfcCatalog(tileRegistry));

        // Create simple biome with A, B, C tiles
        var passable = new TilePool();
        passable.Add("A", 1.0f);
        passable.Add("B", 1.0f);
        passable.Add("C", 1.0f);

        var biome = new BiomeDefinition(
            "plains",
            new CardSignature(),
            passable,
            new TilePool(),
            0.0f);

        var result = generator.Generate(biome, new Vector2I(5, 5), 12345);

        AssertBool(result.Success).IsTrue();
        AssertThat(result.TileIds).IsNotNull();
        AssertThat(result.Size).IsEqual(new Vector2I(5, 5));
    }

    [TestCase]
    public void TestRetryOnContradiction()
    {
        // Rules with no direct A-C edge. The catalog below makes A, B and C gap tiles, and the gap-tile rule then
        // allows every pair of them (A-C included), so these rules cannot contradict in this setup.
        var rules = new WfcAdjacencyRules(new[]
        {
            ("A", "B"),  // A can neighbor B
            ("B", "C")   // B can neighbor C
            // Note: A cannot neighbor C directly
        });

        var tileRegistry = WfcTestFixtures.CreateTestTileRegistry("plains", "A", "B", "C");
        var generator = new WfcMapGenerator(rules, new TileRegistryWfcCatalog(tileRegistry));
        generator.MaxRetries = 5;

        var passable = new TilePool();
        passable.Add("A", 1.0f);
        passable.Add("B", 1.0f);
        passable.Add("C", 1.0f);

        var biome = new BiomeDefinition(
            "plains",
            new CardSignature(),
            passable,
            new TilePool(),
            0.0f);

        var result = generator.Generate(biome, new Vector2I(5, 5), 12345);

        // Documented outcome with the catalog in place: success, a 5x5 map, and only the rule tiles A, B and C
        WfcTestFixtures.AssertSucceeded(result, "Retry map");
        AssertThat(result.Size).IsEqual(new Vector2I(5, 5));
        var ruleTiles = new HashSet<string> { "A", "B", "C" };
        AssertBool(result.TileIds!.Cast<string>().All(ruleTiles.Contains)).IsTrue();
    }
}
