using System.Collections.Generic;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;
using CardCleaner.Tests.Mocks;
using Godot;


namespace CardCleaner.Tests.Core.Services.VariationGroupScenarios;

/// <summary>
///     Tests that PerGeneration variant selection excludes non-selected variants during WFC tile
///     placement; split out of VariationGroupTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TileProbabilityConstraintTest
{
    // ==================== TileProbabilityConstraint Tests ====================
    // These tests verify that PerGeneration variant selection correctly excludes
    // non-selected variants during WFC tile placement.

    [TestCase]
    public void TestTileProbabilityConstraint_PerGenerationVariant_SelectedGetsFullWeight()
    {
        // Setup: Create a mock registry with grass variants in a PerGeneration group
        var registry = new MockTileRegistry();
        registry.RegisterTile(CreateGrassTile("grass1", 0.8f));
        registry.RegisterTile(CreateGrassTile("grass2", 1.0f));
        registry.RegisterTile(CreateGrassTile("grass3", 0.6f));

        // Add the variation group
        var grassGroup = new VariationGroup("grass", VariationMode.PerGeneration, new[]
        {
            new VariantWeight("grass1", 0.8f),
            new VariantWeight("grass2", 1.0f),
            new VariantWeight("grass3", 0.6f)
        });
        registry.AddVariationGroup(grassGroup);

        // Create constraint and set selected variant
        var constraint = new TileProbabilityConstraintForTest(registry);
        constraint.SetSelectedVariants(new Dictionary<string, string> { { "grass", "grass2" } });

        // Create mock context for the selected variant
        var context = CreateMockContext("grass2");

        // Act
        var modifier = constraint.GetProbabilityModifier(context);

        // Assert: Selected variant should get positive weight (group's max weight = 1.0)
        AssertThat(modifier).IsEqual(1.0f);
    }

    [TestCase]
    public void TestTileProbabilityConstraint_PerGenerationVariant_NonSelectedGetsZero()
    {
        // Setup: Create a mock registry with grass variants
        var registry = new MockTileRegistry();
        registry.RegisterTile(CreateGrassTile("grass1", 0.8f));
        registry.RegisterTile(CreateGrassTile("grass2", 1.0f));
        registry.RegisterTile(CreateGrassTile("grass3", 0.6f));

        var grassGroup = new VariationGroup("grass", VariationMode.PerGeneration, new[]
        {
            new VariantWeight("grass1", 0.8f),
            new VariantWeight("grass2", 1.0f),
            new VariantWeight("grass3", 0.6f)
        });
        registry.AddVariationGroup(grassGroup);

        // Create constraint and set grass2 as selected
        var constraint = new TileProbabilityConstraintForTest(registry);
        constraint.SetSelectedVariants(new Dictionary<string, string> { { "grass", "grass2" } });

        // Create mock context for NON-selected variants
        var context1 = CreateMockContext("grass1");
        var context3 = CreateMockContext("grass3");

        // Act
        var modifier1 = constraint.GetProbabilityModifier(context1);
        var modifier3 = constraint.GetProbabilityModifier(context3);

        // Assert: Non-selected variants should get ZERO weight (hard exclusion)
        AssertThat(modifier1).IsEqual(0f);
        AssertThat(modifier3).IsEqual(0f);
    }

    [TestCase]
    public void TestTileProbabilityConstraint_NoSelectionMade_AllVariantsAllowed()
    {
        // Setup: Create registry with PerGeneration group but DON'T set selected variants
        var registry = new MockTileRegistry();
        registry.RegisterTile(CreateGrassTile("grass1", 0.8f));
        registry.RegisterTile(CreateGrassTile("grass2", 1.0f));

        var grassGroup = new VariationGroup("grass", VariationMode.PerGeneration, new[]
        {
            new VariantWeight("grass1", 0.8f),
            new VariantWeight("grass2", 1.0f)
        });
        registry.AddVariationGroup(grassGroup);

        // Create constraint WITHOUT setting selected variants
        var constraint = new TileProbabilityConstraintForTest(registry);
        // constraint.SetSelectedVariants(null); // Not called

        // Create contexts for both variants
        var context1 = CreateMockContext("grass1");
        var context2 = CreateMockContext("grass2");

        // Act
        var modifier1 = constraint.GetProbabilityModifier(context1);
        var modifier2 = constraint.GetProbabilityModifier(context2);

        // Assert: Both should get positive weight (fallback behavior = group max weight)
        AssertThat(modifier1).IsGreater(0f);
        AssertThat(modifier2).IsGreater(0f);
    }

    [TestCase]
    public void TestTileProbabilityConstraint_PerInstanceVariant_AllVariantsAllowed()
    {
        // Setup: Create registry with PerInstance group (not PerGeneration)
        var registry = new MockTileRegistry();
        registry.RegisterTile(CreateFlowerTile("flower_a", 0.4f));
        registry.RegisterTile(CreateFlowerTile("flower_b", 0.6f));

        var flowerGroup = new VariationGroup("flower", VariationMode.PerInstance, new[]
        {
            new VariantWeight("flower_a", 0.4f),
            new VariantWeight("flower_b", 0.6f)
        });
        registry.AddVariationGroup(flowerGroup);

        // Create constraint - even with selection set, PerInstance should ignore it
        var constraint = new TileProbabilityConstraintForTest(registry);
        constraint.SetSelectedVariants(new Dictionary<string, string> { { "flower", "flower_a" } });

        var contextA = CreateMockContext("flower_a");
        var contextB = CreateMockContext("flower_b");

        // Act
        var modifierA = constraint.GetProbabilityModifier(contextA);
        var modifierB = constraint.GetProbabilityModifier(contextB);

        // Assert: PerInstance variants are all allowed (selection only affects PerGeneration)
        AssertThat(modifierA).IsGreater(0f);
        AssertThat(modifierB).IsGreater(0f);
    }

    private static TileDefinition CreateGrassTile(string id, float probability)
    {
        return new TileDefinition(
            id,
            id,
            TilePassability.Passable,
            Vector2I.Zero,
            new TileDefinitionOptions
            {
                SourceId = 0,
                Layer = TileLayer.Terrain,
                Elevation = 0,
                IsTransparent = true,
                AllowedBiomes = null,
                Size = null,
                DecorationDensity = 1f,
                AutoTileVariants = new Vector2I?[16], // Has auto-tile variants
                AutoTileFormatName = "corner16",
                Variations = null,
                VariationMode = VariationMode.PerGeneration,
                Animation = null,
                Dominance = 0,
                InnerTerrainId = null,
                OuterTerrainId = null,
                IsGapTile = false,
                Probability = probability
            });
    }

    private static TileDefinition CreateFlowerTile(string id, float probability)
    {
        return new TileDefinition(
            id,
            id,
            TilePassability.Passable,
            Vector2I.Zero,
            new TileDefinitionOptions
            {
                SourceId = 0,
                Layer = TileLayer.Decoration,
                Elevation = 0,
                IsTransparent = true,
                AllowedBiomes = null,
                Size = null,
                DecorationDensity = 1f,
                AutoTileVariants = null, // No auto-tile variants
                AutoTileFormatName = null,
                Variations = null,
                VariationMode = VariationMode.PerInstance,
                Animation = null,
                Dominance = 0,
                InnerTerrainId = null,
                OuterTerrainId = null,
                IsGapTile = false,
                Probability = probability
            });
    }

    private static WfcConstraintContext CreateMockContext(string tileId)
    {
        // Create a minimal mock WFC grid for context
        var grid = new WfcGrid(1, 1, new HashSet<string> { tileId });
        return new WfcConstraintContext
        {
            TileId = tileId,
            CellId = 0,
            Topology = grid,
            Rng = null,
            NeighborInfo = null
        };
    }
}
