using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.Wfc.BiomeGridScenarios;

/// <summary>
///     BiomeGridGenerationTest scenarios split out of BiomeGridIntegrationTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public partial class BiomeGridGenerationTest : BiomeGridIntegrationTestBase
{
    [TestCase]
    public void GenerateMultiBiome_WithGradient_CreatesBiomeGrid()
    {
        // Arrange
        var generator = new WfcMapGenerator(_rules, new TileRegistryWfcCatalog(_tileRegistry));
        var gradient = new FixedGradient(new CardSignature(new[] { 0f, 0.5f, 0f, 0f, 0f, 0f, 0f, 0f }));

        // Act
        var result = generator.GenerateMultiBiome(
            _registry,
            pos => _registry.GetBiome("fire")!,
            new Vector2I(5, 5),
            12345,
            gradient);

        // Assert
        // Should succeed (BiomeGrid is created and BiomeAffinityConstraint is registered)
        AssertBool(result.Success).IsTrue();
        AssertThat(result.TileIds).IsNotNull();
        AssertThat(result.Size).IsEqual(new Vector2I(5, 5));
    }

    [TestCase]
    public void GenerateMultiBiome_WithoutGradient_StillWorks()
    {
        // Arrange
        var generator = new WfcMapGenerator(_rules, new TileRegistryWfcCatalog(_tileRegistry));

        // Act: Pass null gradient (backwards compatible)
        var result = generator.GenerateMultiBiome(
            _registry,
            pos => _registry.GetBiome("fire")!,
            new Vector2I(5, 5),
            12345,
            null);

        // Assert: Should succeed without BiomeAffinityConstraint
        AssertBool(result.Success).IsTrue();
        AssertThat(result.TileIds).IsNotNull();
    }
}
