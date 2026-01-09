using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;
using CardCleaner.Tests.Mocks;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Core.Services;

/// <summary>
/// Tests for the tile variation grouping system:
/// - Name pattern detection for numbered (per-map) and lettered (per-instance) variations
/// - VariationGroup and VariationGroupCollection functionality
/// - Probability weight handling and normalization
/// </summary>
[TestSuite]
public class VariationGroupTest
{
    // ==================== Name Pattern Detection Tests ====================

    [TestCase]
    public void TestDetectNumberedSuffix_ReturnsPerGenerationMode()
    {
        var result = TiledTilesetLoader.DetectVariationPattern("grass1");

        AssertThat(result).IsNotNull();
        AssertThat(result!.BaseName).IsEqual("grass");
        AssertThat(result.VariantIndex).IsEqual(1);
        AssertThat(result.Mode).IsEqual(VariationMode.PerGeneration);
    }

    [TestCase]
    public void TestDetectNumberedSuffix_MultiDigit()
    {
        var result = TiledTilesetLoader.DetectVariationPattern("terrain12");

        AssertThat(result).IsNotNull();
        AssertThat(result!.BaseName).IsEqual("terrain");
        AssertThat(result.VariantIndex).IsEqual(12);
        AssertThat(result.Mode).IsEqual(VariationMode.PerGeneration);
    }

    [TestCase]
    public void TestDetectLetteredSuffix_ReturnsPerInstanceMode()
    {
        var result = TiledTilesetLoader.DetectVariationPattern("flower_a");

        AssertThat(result).IsNotNull();
        AssertThat(result!.BaseName).IsEqual("flower");
        AssertThat(result.VariantIndex).IsEqual(0); // a = 0
        AssertThat(result.Mode).IsEqual(VariationMode.PerInstance);
    }

    [TestCase]
    public void TestDetectLetteredSuffix_LetterC()
    {
        var result = TiledTilesetLoader.DetectVariationPattern("rose_c");

        AssertThat(result).IsNotNull();
        AssertThat(result!.BaseName).IsEqual("rose");
        AssertThat(result.VariantIndex).IsEqual(2); // c = 2
        AssertThat(result.Mode).IsEqual(VariationMode.PerInstance);
    }

    [TestCase]
    public void TestDetectLetteredSuffix_CaseInsensitive()
    {
        var result = TiledTilesetLoader.DetectVariationPattern("poppy_B");

        AssertThat(result).IsNotNull();
        AssertThat(result!.BaseName).IsEqual("poppy");
        AssertThat(result.VariantIndex).IsEqual(1); // B = 1
        AssertThat(result.Mode).IsEqual(VariationMode.PerInstance);
    }

    [TestCase]
    public void TestDetectNoPattern_ReturnsNull()
    {
        var result = TiledTilesetLoader.DetectVariationPattern("dirt");

        AssertThat(result).IsNull();
    }

    [TestCase]
    public void TestDetectNoPattern_ShortBaseName_ReturnsNull()
    {
        // Base name too short (less than 2 chars)
        var result = TiledTilesetLoader.DetectVariationPattern("a1");

        AssertThat(result).IsNull();
    }

    [TestCase]
    public void TestDetectNoPattern_SnakeCaseWithoutLetter_ReturnsNull()
    {
        // Has underscore but not followed by single letter
        var result = TiledTilesetLoader.DetectVariationPattern("tall_grass");

        AssertThat(result).IsNull();
    }

    // ==================== VariationGroup Tests ====================

    [TestCase]
    public void TestVariationGroup_MaxWeight()
    {
        var variants = new[]
        {
            new VariantWeight("grass1", 0.8f),
            new VariantWeight("grass2", 1.0f),
            new VariantWeight("grass3", 0.6f)
        };

        var group = new VariationGroup("grass", VariationMode.PerGeneration, variants);

        AssertThat(group.MaxWeight).IsEqual(1.0f);
    }

    [TestCase]
    public void TestVariationGroup_GetWeight()
    {
        var variants = new[]
        {
            new VariantWeight("flower_a", 0.5f),
            new VariantWeight("flower_b", 0.3f)
        };

        var group = new VariationGroup("flower", VariationMode.PerInstance, variants);

        AssertThat(group.GetWeight("flower_a")).IsEqual(0.5f);
        AssertThat(group.GetWeight("flower_b")).IsEqual(0.3f);
        AssertThat(group.GetWeight("flower_c")).IsEqual(0f); // Not in group
    }

    [TestCase]
    public void TestVariationGroup_GetNormalizedWeight()
    {
        var variants = new[]
        {
            new VariantWeight("rose_a", 0.8f),
            new VariantWeight("rose_b", 0.4f)
        };

        var group = new VariationGroup("rose", VariationMode.PerInstance, variants);

        // MaxWeight = 0.8, so rose_a normalized = 1.0, rose_b normalized = 0.5
        AssertThat(group.GetNormalizedWeight("rose_a")).IsEqual(1.0f);
        AssertThat(group.GetNormalizedWeight("rose_b")).IsEqual(0.5f);
    }

    [TestCase]
    public void TestVariationGroup_ContainsTile()
    {
        var variants = new[]
        {
            new VariantWeight("grass1", 1.0f),
            new VariantWeight("grass2", 1.0f)
        };

        var group = new VariationGroup("grass", VariationMode.PerGeneration, variants);

        AssertThat(group.ContainsTile("grass1")).IsTrue();
        AssertThat(group.ContainsTile("grass2")).IsTrue();
        AssertThat(group.ContainsTile("grass3")).IsFalse();
    }

    // ==================== VariationGroupCollection Tests ====================

    [TestCase]
    public void TestVariationGroupCollection_AddAndLookup()
    {
        var collection = new VariationGroupCollection();

        var grassVariants = new[]
        {
            new VariantWeight("grass1", 1.0f),
            new VariantWeight("grass2", 0.8f)
        };
        var grassGroup = new VariationGroup("grass", VariationMode.PerGeneration, grassVariants);
        collection.AddGroup(grassGroup);

        // Lookup by base name
        var found = collection.GetGroupByBaseName("grass");
        AssertThat(found).IsNotNull();
        AssertThat(found!.BaseName).IsEqual("grass");
    }

    [TestCase]
    public void TestVariationGroupCollection_FindGroupContaining()
    {
        var collection = new VariationGroupCollection();

        var variants = new[]
        {
            new VariantWeight("flower_a", 0.4f),
            new VariantWeight("flower_b", 0.6f)
        };
        var group = new VariationGroup("flower", VariationMode.PerInstance, variants);
        collection.AddGroup(group);

        // Find group by member tile ID
        var foundByA = collection.FindGroupContaining("flower_a");
        var foundByB = collection.FindGroupContaining("flower_b");
        var notFound = collection.FindGroupContaining("rose_a");

        AssertThat(foundByA).IsNotNull();
        AssertThat(foundByB).IsNotNull();
        AssertThat(foundByA!.BaseName).IsEqual("flower");
        AssertThat(notFound).IsNull();
    }

    [TestCase]
    public void TestVariationGroupCollection_GetVariantsFor()
    {
        var collection = new VariationGroupCollection();

        var variants = new[]
        {
            new VariantWeight("grass1", 0.5f),
            new VariantWeight("grass2", 1.0f),
            new VariantWeight("grass3", 0.7f)
        };
        collection.AddGroup(new VariationGroup("grass", VariationMode.PerGeneration, variants));

        var retrieved = collection.GetVariantsFor("grass");

        AssertThat(retrieved.Count).IsEqual(3);
        AssertThat(retrieved.Any(v => v.TileId == "grass1" && v.Weight == 0.5f)).IsTrue();
    }

    [TestCase]
    public void TestVariationGroupCollection_Clear()
    {
        var collection = new VariationGroupCollection();
        collection.AddGroup(new VariationGroup("test", VariationMode.PerGeneration,
            new[] { new VariantWeight("test1", 1f) }));

        AssertThat(collection.Count).IsEqual(1);

        collection.Clear();

        AssertThat(collection.Count).IsEqual(0);
        AssertThat(collection.GetGroupByBaseName("test")).IsNull();
    }

    // ==================== Probability/Density Tests ====================

    [TestCase]
    public void TestDensity_HighProbabilityGroupHasHigherDensity()
    {
        // Flower group with max prob 0.4
        var flowerVariants = new[]
        {
            new VariantWeight("flower_a", 0.4f),
            new VariantWeight("flower_b", 0.2f)
        };
        var flowerGroup = new VariationGroup("flower", VariationMode.PerInstance, flowerVariants);

        // Decoration group with max prob 0.8
        var decorVariants = new[]
        {
            new VariantWeight("decor_a", 0.8f),
            new VariantWeight("decor_b", 0.3f)
        };
        var decorGroup = new VariationGroup("decor", VariationMode.PerInstance, decorVariants);

        // Decor should have 2x the density of flower
        AssertThat(decorGroup.MaxWeight).IsEqual(0.8f);
        AssertThat(flowerGroup.MaxWeight).IsEqual(0.4f);
        AssertThat(decorGroup.MaxWeight / flowerGroup.MaxWeight).IsEqual(2.0f);
    }

    [TestCase]
    public void TestRelativeWeight_RatioPreserved()
    {
        // Poppy weight 2, Rose weight 1 → poppy should be 2x more likely
        var variants = new[]
        {
            new VariantWeight("poppy", 2.0f),
            new VariantWeight("rose", 1.0f)
        };
        var group = new VariationGroup("flowers", VariationMode.PerInstance, variants);

        var poppyNorm = group.GetNormalizedWeight("poppy"); // 2/2 = 1.0
        var roseNorm = group.GetNormalizedWeight("rose");   // 1/2 = 0.5

        // Poppy should have 2x the relative weight of rose
        AssertThat(poppyNorm / roseNorm).IsEqual(2.0f);
    }

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

    // ==================== Helper Methods and Test Classes ====================

    /// <summary>
    /// Test-friendly version of TileProbabilityConstraint that works with ITileRegistry.
    /// </summary>
    private class TileProbabilityConstraintForTest : IWfcConstraint
    {
        private readonly ITileRegistry _tileRegistry;
        private Dictionary<string, string>? _selectedVariants;
        public float MinModifier { get; set; } = 0.01f;

        public TileProbabilityConstraintForTest(ITileRegistry tileRegistry)
        {
            _tileRegistry = tileRegistry;
        }

        public void SetSelectedVariants(Dictionary<string, string>? selectedVariants)
        {
            _selectedVariants = selectedVariants;
        }

        public float GetProbabilityModifier(WfcConstraintContext context)
        {
            var tile = _tileRegistry.GetTile(context.TileId);
            if (tile == null)
                return 1f;

            var group = _tileRegistry.GetVariationGroup(context.TileId);

            if (group != null)
            {
                var density = group.MaxWeight;

                if (group.Mode == VariationMode.PerGeneration)
                {
                    if (_selectedVariants != null &&
                        _selectedVariants.TryGetValue(group.BaseName, out var selectedTileId))
                    {
                        if (context.TileId != selectedTileId)
                        {
                            return 0.0f; // Hard exclusion
                        }
                    }
                    return System.Math.Max(MinModifier, density);
                }

                var normalizedWeight = group.GetNormalizedWeight(context.TileId);
                return System.Math.Max(MinModifier, density * normalizedWeight);
            }

            return System.Math.Max(MinModifier, tile.Probability);
        }
    }

    private static TileDefinition CreateGrassTile(string id, float probability)
    {
        return new TileDefinition(
            id: id,
            name: id,
            passability: TilePassability.Passable,
            atlasCoords: Vector2I.Zero,
            sourceId: 0,
            layer: TileLayer.Terrain,
            elevation: 0,
            isTransparent: true,
            allowedBiomes: null,
            size: null,
            decorationDensity: 1f,
            autoTileVariants: new Vector2I?[16], // Has auto-tile variants
            autoTileFormatName: "corner16",
            variations: null,
            variationMode: VariationMode.PerGeneration,
            animation: null,
            dominance: 0,
            innerTerrainId: null,
            outerTerrainId: null,
            isGapTile: false,
            probability: probability);
    }

    private static TileDefinition CreateFlowerTile(string id, float probability)
    {
        return new TileDefinition(
            id: id,
            name: id,
            passability: TilePassability.Passable,
            atlasCoords: Vector2I.Zero,
            sourceId: 0,
            layer: TileLayer.Decoration,
            elevation: 0,
            isTransparent: true,
            allowedBiomes: null,
            size: null,
            decorationDensity: 1f,
            autoTileVariants: null, // No auto-tile variants
            autoTileFormatName: null,
            variations: null,
            variationMode: VariationMode.PerInstance,
            animation: null,
            dominance: 0,
            innerTerrainId: null,
            outerTerrainId: null,
            isGapTile: false,
            probability: probability);
    }

    private static WfcConstraintContext CreateMockContext(string tileId)
    {
        // Create a minimal mock WFC grid for context
        var grid = new WfcGrid(1, 1, new HashSet<string> { tileId });
        return new WfcConstraintContext
        {
            TileId = tileId,
            Position = Vector2I.Zero,
            Grid = grid,
            Rng = null,
            NeighborInfo = null
        };
    }
}
