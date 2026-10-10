using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.Wfc.BiomeGridScenarios;

/// <summary>
///     BiomeGridTileAffinityTest scenarios split out of BiomeGridIntegrationTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public partial class BiomeGridTileAffinityTest : BiomeGridIntegrationTestBase
{
    [TestCase]
    public void TileAffinity_DefaultsToPositiveForBiomeTiles()
    {
        // This test verifies that tiles in a biome's TilePool get boosted
        // when the gradient matches the biome's signature.

        // Arrange: Gradient matches fire biome signature
        var generator = new WfcMapGenerator(_rules, new TileRegistryWfcCatalog(_tileRegistry));
        var fireMatchingGradient = new FixedGradient(new CardSignature(new[] { 0f, 1f, 0f, 0f, 0f, 0f, 0f, 0f }));

        // Act: Generate map with fire-matching gradient at fire biome positions
        var result = generator.GenerateMultiBiome(
            _registry,
            pos => _registry.GetBiome("fire")!,
            new Vector2I(8, 8),
            42,
            fireMatchingGradient);

        // Assert: Map should generate successfully
        AssertBool(result.Success).IsTrue();
        AssertThat(result.TileIds).IsNotNull();

        // Count fire tiles - with matching gradient, fire tiles should be boosted
        var fireTileCount = 0;
        var tileIds = result.TileIds!;
        for (var y = 0; y < result.Size.Y; y++)
        {
            for (var x = 0; x < result.Size.X; x++)
            {
                if (tileIds[y, x] == "fire_tile")
                    fireTileCount++;
            }
        }

        // Fire tiles should appear (boosted by positive affinity)
        AssertThat(fireTileCount).IsGreater(0);
        GD.Print($"Fire tiles with matching gradient: {fireTileCount}/{result.Size.X * result.Size.Y}");
    }

    [TestCase]
    public void TileAffinity_DefaultsToNeutralForOtherTiles()
    {
        // Tiles not in any biome's TilePool should get neutral modifier (1.0)

        // Arrange: Add a neutral tile to rules but not to any biome
        var rulesWithNeutral = new WfcAdjacencyRules(new[]
        {
            ("fire_tile", "fire_tile"),
            ("fire_tile", "water_tile"),
            ("fire_tile", "neutral_tile"),
            ("water_tile", "water_tile"),
            ("water_tile", "neutral_tile"),
            ("neutral_tile", "neutral_tile")
        });

        // The fixture's tile and biome registries already hold fire/water/neutral tiles and the matching biomes
        var generator = new WfcMapGenerator(rulesWithNeutral, new TileRegistryWfcCatalog(_tileRegistry));
        var gradient = new FixedGradient(new CardSignature()); // Neutral signature

        // Act
        var result = generator.GenerateMultiBiome(
            _registry,
            pos => _registry.GetBiome("fire")!,
            new Vector2I(8, 8),
            12345,
            gradient);

        // Assert: Should succeed - neutral_tile gets modifier 1.0 (not penalized)
        AssertBool(result.Success).IsTrue();
    }
}
