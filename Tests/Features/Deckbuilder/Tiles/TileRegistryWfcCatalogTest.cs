using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Tests.Mocks;
using Godot;

namespace CardCleaner.Tests.Features.Deckbuilder.Tiles;

/// <summary>
/// Pins the answers TileRegistryWfcCatalog gives for the registry shapes WFC meets: unknown ids, known tiles,
/// gap tiles, auto-tiles with and without a solid-fill variant, and variation groups.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TileRegistryWfcCatalogTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void TileRegisteredAfterConstruction_IsSeenByContainsAndTileIds()
    {
        var registry = new MockTileRegistry();
        var catalog = new TileRegistryWfcCatalog(registry);

        registry.RegisterTile(MockTileRegistry.CreateGapTile("dirt"));

        AssertBool(catalog.Contains("dirt")).IsTrue();
        AssertThat(catalog.TileIds.ToArray()).IsEqual(new[] { "dirt" });
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void UnknownTile_IsNotAutoTileAndHasNoSolidFill()
    {
        var catalog = new TileRegistryWfcCatalog(new MockTileRegistry());

        AssertBool(catalog.IsAutoTile("missing")).IsFalse();
        AssertBool(catalog.HasSolidFillVariant("missing")).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void GapTile_IsNotAutoTileAndHasNoSolidFill()
    {
        var registry = new MockTileRegistry();
        registry.RegisterTile(MockTileRegistry.CreateGapTile("dirt"));
        var catalog = new TileRegistryWfcCatalog(registry);

        AssertBool(catalog.IsAutoTile("dirt")).IsFalse();
        AssertBool(catalog.HasSolidFillVariant("dirt")).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void AutoTileWithoutVariantFifteen_HasNoSolidFill()
    {
        var registry = new MockTileRegistry();
        registry.RegisterTile(MockTileRegistry.CreateAutoTile("grass"));
        var catalog = new TileRegistryWfcCatalog(registry);

        AssertBool(catalog.IsAutoTile("grass")).IsTrue();
        AssertBool(catalog.HasSolidFillVariant("grass")).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void AutoTileWithVariantFifteen_HasSolidFill()
    {
        var variants = new Vector2I?[16];
        variants[15] = Vector2I.Zero;
        var registry = new MockTileRegistry();
        registry.RegisterTile(new TileDefinition(
            "stone",
            "Stone",
            TilePassability.Passable,
            Vector2I.Zero,
            new TileDefinitionOptions { AutoTileVariants = variants }));
        var catalog = new TileRegistryWfcCatalog(registry);

        AssertBool(catalog.IsAutoTile("stone")).IsTrue();
        AssertBool(catalog.HasSolidFillVariant("stone")).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void UnknownTile_HasNoBitmaskOrMultiCellAnswer()
    {
        var catalog = new TileRegistryWfcCatalog(new MockTileRegistry());

        AssertBool(catalog.IsBitmaskAllowed("missing", 0) == null).IsTrue();
        AssertBool(catalog.GetMultiCellBounds("missing") == null).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void PerInstanceVariationGroup_GivesDensityAndNormalizedWeight()
    {
        var registry = new MockTileRegistry();
        registry.RegisterTile(MockTileRegistry.CreateGapTile("flower1"));
        registry.RegisterTile(MockTileRegistry.CreateGapTile("flower2"));
        registry.RegisterTile(MockTileRegistry.CreateGapTile("plain"));
        registry.AddVariationGroup(new VariationGroup("flower", VariationMode.PerInstance, new[]
        {
            new VariantWeight("flower1", 2f),
            new VariantWeight("flower2", 1f)
        }));
        var catalog = new TileRegistryWfcCatalog(registry);

        var variation = catalog.GetVariation("flower2");

        AssertBool(variation.HasValue).IsTrue();
        var group = variation.GetValueOrDefault();
        AssertString(group.BaseName).IsEqual("flower");
        AssertBool(group.IsPerGeneration).IsFalse();
        AssertFloat(group.Density).IsEqual(2f);
        AssertFloat(group.NormalizedWeight).IsEqual(0.5f);
        AssertBool(catalog.GetVariation("plain") == null).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void UnknownTile_AnswersAreNeutral()
    {
        var catalog = new TileRegistryWfcCatalog(new MockTileRegistry());

        AssertBool(catalog.Contains("missing")).IsFalse();
        AssertBool(catalog.IsPassable("missing")).IsFalse();
        AssertFloat(catalog.GetProbability("missing")).IsEqual(1f);
        AssertBool(catalog.HasBiomeRestriction("missing")).IsFalse();
        AssertBool(catalog.IsAllowedInBiome("missing", "forest")).IsFalse();
        AssertBool(catalog.AreSameTerrainType("missing", "grass")).IsFalse();
        AssertBool(catalog.AreSameTerrainType("missing", "missing")).IsTrue();
        AssertBool(catalog.GetVariation("missing") == null).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void KnownTile_ReadsPassabilityProbabilityBiomeAndTerrainType()
    {
        var registry = new MockTileRegistry();
        registry.RegisterTile(new TileDefinition(
            "meadow",
            "Meadow",
            TilePassability.Passable,
            Vector2I.Zero,
            new TileDefinitionOptions
            {
                Probability = 0.25f,
                AllowedBiomes = new HashSet<string> { "forest" }
            }));
        registry.RegisterTile(MockTileRegistry.CreateGapTile("dirt"));
        registry.RegisterTile(MockTileRegistry.CreateGapTile("flower1"));
        registry.RegisterTile(MockTileRegistry.CreateGapTile("flower2"));
        registry.AddVariationGroup(new VariationGroup("flower", VariationMode.PerInstance, new[]
        {
            new VariantWeight("flower1", 2f),
            new VariantWeight("flower2", 1f)
        }));
        var catalog = new TileRegistryWfcCatalog(registry);

        AssertBool(catalog.IsPassable("meadow")).IsTrue();
        AssertFloat(catalog.GetProbability("meadow")).IsEqual(0.25f);
        AssertBool(catalog.HasBiomeRestriction("meadow")).IsTrue();
        AssertBool(catalog.IsAllowedInBiome("meadow", "forest")).IsTrue();
        AssertBool(catalog.IsAllowedInBiome("meadow", "desert")).IsFalse();
        AssertBool(catalog.HasBiomeRestriction("dirt")).IsFalse();
        AssertBool(catalog.IsAllowedInBiome("dirt", "desert")).IsTrue();
        AssertBool(catalog.AreSameTerrainType("meadow", "meadow")).IsTrue();
        AssertBool(catalog.AreSameTerrainType("flower1", "flower2")).IsTrue();
        AssertBool(catalog.AreSameTerrainType("meadow", "dirt")).IsFalse();
    }
}
