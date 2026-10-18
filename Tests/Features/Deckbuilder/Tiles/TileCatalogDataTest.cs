using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Randomness;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Tests.Mocks;
using Godot;

namespace CardCleaner.Tests.Features.Deckbuilder.Tiles;

/// <summary>
/// Pins TileCatalogData, the immutable snapshot of a registry that WFC reads. The snapshot must answer like the
/// registry it was taken from, keep the registry's order, pick per-map variants the way the registry does, and leave
/// out tiles registered after it was taken. The shape answers and the unknown-id answers of the deleted
/// TileRegistryWfcCatalog are kept here.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TileCatalogDataTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void SnapshotAnswersLikeTheRegistryItWasTakenFrom()
    {
        var registry = MockTileRegistry.CreateWithTestTiles();
        var catalog = TileCatalogData.From(registry);

        AssertThat(Ids(catalog.GetAllTiles())).IsEqual(Ids(registry.GetAllTiles()));
        AssertThat(Ids(catalog.GetSimpleTerrainTiles())).IsEqual(Ids(registry.GetSimpleTerrainTiles()));
        AssertThat(Ids(catalog.GetAutoTiles())).IsEqual(Ids(registry.GetAutoTiles()));
        AssertThat(Ids(catalog.GetGapTiles())).IsEqual(Ids(registry.GetGapTiles()));
        AssertThat(Ids(catalog.GetPassableTerrainTiles())).IsEqual(Ids(registry.GetPassableTerrainTiles()));
        AssertThat(Ids(catalog.GetSolidTerrainTiles())).IsEqual(Ids(registry.GetSolidTerrainTiles()));
        AssertThat(Ids(catalog.GetDecorationTiles())).IsEqual(Ids(registry.GetDecorationTiles()));
        AssertThat(Ids(catalog.GetBackgroundTerrainTiles())).IsEqual(Ids(registry.GetBackgroundTerrainTiles()));
        AssertThat(Ids(catalog.GetTilesByLayer(TileLayer.Terrain)))
            .IsEqual(Ids(registry.GetTilesByLayer(TileLayer.Terrain)));
        AssertThat(Ids(catalog.GetTilesByBiome("any"))).IsEqual(Ids(registry.GetTilesByBiome("any")));
        AssertThat(catalog.GetWfcTileIds().ToArray()).IsEqual(registry.GetWfcTileIds().ToArray());
        AssertThat(catalog.GetDefaultPassableTileId()).IsEqual(registry.GetDefaultPassableTileId());
        AssertThat(catalog.GetDefaultSolidTileId()).IsEqual(registry.GetDefaultSolidTileId());
        AssertThat(catalog.GetDefaultGapTileId()).IsEqual(registry.GetDefaultGapTileId());

        foreach (var id in new[] { "grass", "stone", "dirt", "rock", "missing" })
        {
            AssertBool(catalog.IsAutoTile(id)).IsEqual(registry.IsAutoTile(id));
            AssertBool(catalog.IsPassable(id)).IsEqual(registry.IsPassable(id));
            AssertBool(catalog.IsSolid(id)).IsEqual(registry.IsSolid(id));
            AssertBool(catalog.IsGapTile(id)).IsEqual(registry.IsGapTile(id));
            AssertBool(catalog.IsTransparent(id)).IsEqual(registry.IsTransparent(id));
        }

        AssertBool(catalog.AreSameTerrainType("grass", "stone")).IsEqual(registry.AreSameTerrainType("grass", "stone"));
        AssertBool(catalog.AreSameTerrainType("dirt", "rock")).IsEqual(registry.AreSameTerrainType("dirt", "rock"));
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void AllTilesKeepTheRegistryInsertionOrder()
    {
        string[] order = { "sand", "grass", "water", "stone", "dirt", "rock" };
        var registry = new MockTileRegistry();
        foreach (var id in order)
            registry.RegisterTile(MockTileRegistry.CreateGapTile(id));

        var catalog = TileCatalogData.From(registry);

        AssertThat(Ids(catalog.GetAllTiles())).IsEqual(order);
        AssertThat(catalog.TileIds.ToArray()).IsEqual(order);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void SelectPerMapVariantMatchesTheSelectorAndLeavesTheRngAtTheSameStep()
    {
        var registry = new MockTileRegistry();
        registry.RegisterTile(MockTileRegistry.CreateGapTile("flower1"));
        registry.RegisterTile(MockTileRegistry.CreateGapTile("flower2"));
        registry.AddVariationGroup(new VariationGroup("flower", VariationMode.PerGeneration, new[]
        {
            new VariantWeight("flower1", 2f),
            new VariantWeight("flower2", 1f)
        }));
        var catalog = TileCatalogData.From(registry);

        var catalogRng = RandomSources.Create(42UL);
        var expectedRng = RandomSources.Create(42UL);

        var picked = catalog.SelectPerMapVariant("flower", catalogRng);
        var expected = WeightedVariantSelector.Select(registry.GetVariationGroupByBaseName("flower"), expectedRng);

        AssertBool(picked != null).IsTrue();
        AssertThat(picked).IsEqual(expected);
        AssertThat(catalogRng.Randi()).IsEqual(expectedRng.Randi());
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void UnknownIdAnswersAreNeutral()
    {
        var catalog = TileCatalogData.From(MockTileRegistry.CreateWithTestTiles());

        AssertBool(catalog.GetTile("missing") == null).IsTrue();
        AssertBool(catalog.Contains("missing")).IsFalse();
        AssertBool(catalog.IsAutoTile("missing")).IsFalse();
        AssertBool(catalog.IsPassable("missing")).IsFalse();
        AssertBool(catalog.HasSolidFillVariant("missing")).IsFalse();
        AssertFloat(catalog.GetProbability("missing")).IsEqual(1f);
        AssertBool(catalog.GetVariation("missing") == null).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TileRegisteredAfterTheSnapshotIsNotVisible()
    {
        var registry = new MockTileRegistry();
        var catalog = TileCatalogData.From(registry);

        registry.RegisterTile(MockTileRegistry.CreateGapTile("dirt"));

        AssertBool(catalog.Contains("dirt")).IsFalse();
        AssertBool(catalog.GetTile("dirt") == null).IsTrue();
        AssertThat(catalog.TileIds.ToArray()).IsEqual(Array.Empty<string>());
        AssertThat(catalog.GetAllTiles().Count).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void UnknownTile_IsNotAutoTileAndHasNoSolidFill()
    {
        var catalog = TileCatalogData.From(new MockTileRegistry());

        AssertBool(catalog.IsAutoTile("missing")).IsFalse();
        AssertBool(catalog.HasSolidFillVariant("missing")).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void GapTile_IsNotAutoTileAndHasNoSolidFill()
    {
        var registry = new MockTileRegistry();
        registry.RegisterTile(MockTileRegistry.CreateGapTile("dirt"));
        var catalog = TileCatalogData.From(registry);

        AssertBool(catalog.IsAutoTile("dirt")).IsFalse();
        AssertBool(catalog.HasSolidFillVariant("dirt")).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void AutoTileWithoutVariantFifteen_HasNoSolidFill()
    {
        var registry = new MockTileRegistry();
        registry.RegisterTile(MockTileRegistry.CreateAutoTile("grass"));
        var catalog = TileCatalogData.From(registry);

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
        var catalog = TileCatalogData.From(registry);

        AssertBool(catalog.IsAutoTile("stone")).IsTrue();
        AssertBool(catalog.HasSolidFillVariant("stone")).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void UnknownTile_HasNoBitmaskOrMultiCellAnswer()
    {
        var catalog = TileCatalogData.From(new MockTileRegistry());

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
        var catalog = TileCatalogData.From(registry);

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
        var catalog = TileCatalogData.From(new MockTileRegistry());

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
        var catalog = TileCatalogData.From(registry);

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

    [TestCase]
    [TestCategory("Unit")]
    public static void TileWithShapeFacts_AnswersFromTheTileData()
    {
        var registry = new MockTileRegistry();
        registry.RegisterTile(new TileDefinition(
            "shaped",
            "Shaped",
            TilePassability.Passable,
            Vector2I.Zero,
            new TileDefinitionOptions
            {
                AllowedBitmasks = new HashSet<int> { 0, 15 },
                MultiCellBounds = (new Vector2I(1, 3), new Vector2I(0, -2))
            }));
        registry.RegisterTile(new TileDefinition("plain", "Plain", TilePassability.Passable, Vector2I.Zero));
        var catalog = TileCatalogData.From(registry);

        AssertBool(catalog.IsBitmaskAllowed("shaped", 15) == true).IsTrue();
        AssertBool(catalog.IsBitmaskAllowed("shaped", 5) == false).IsTrue();
        AssertBool(catalog.GetMultiCellBounds("shaped") == (new Vector2I(1, 3), new Vector2I(0, -2))).IsTrue();
        AssertBool(catalog.IsBitmaskAllowed("plain", 5) == null).IsTrue();
        AssertBool(catalog.GetMultiCellBounds("plain") == null).IsTrue();
    }

    private static string[] Ids(IEnumerable<TileDefinition> tiles) => tiles.Select(tile => tile.Id).ToArray();
}
