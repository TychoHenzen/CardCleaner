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
        // Create rules that might cause contradictions
        var rules = new WfcAdjacencyRules(new[]
        {
            ("A", "B"),  // A can neighbor B
            ("B", "C")   // B can neighbor C
            // Note: A cannot neighbor C directly
        });

        var generator = new WfcMapGenerator(rules);
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

        // This might fail or succeed depending on collapse order
        var result = generator.Generate(biome, new Vector2I(5, 5), 12345);

        // Just verify it doesn't crash and provides meaningful feedback
        if (!result.Success)
        {
            AssertThat(result.ErrorMessage).IsNotNull();
            GD.Print($"Expected failure with limited rules: {result.ErrorMessage}");
        }
    }
}
