using System.Linq;
using CardCleaner.Scripts.Core.Randomness;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Components.WorldMapTerrainRendererSupport;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using Godot;

namespace CardCleaner.Tests.Features.Deckbuilder.Components;

/// <summary>
/// Pins the dual-grid overlay builder that WorldMapTerrainRenderer uses to turn the two WFC layers into visual cells.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class DualGridOverlayBuilderTest
{
    private static readonly Vector2I GridSize = new(16, 12);

    [TestCase]
    [TestCategory("Unit")]
    public static void SingleAutoTileCellProducesTheExpectedBitmask()
    {
        var registry = new TileRegistry(new GodotAssetPaths(), new CompiledAtlasSource());
        var autoTileId = registry.GetAllTiles().First(tile => tile.HasAutoTileVariants).Id;
        var baseTileId = DualGridOverlayBuilder.ResolveDefaultPassableTileId(registry);
        var background = Filled(3, 3, baseTileId);
        var foreground = Filled(3, 3, string.Empty);
        foreground[1, 1] = autoTileId;

        var builder = new DualGridOverlayBuilder(registry, baseTileId);
        var overlays = builder.Build(background, foreground, new Vector2I(3, 3));

        // The four visual cells whose corner set contains data cell (1,1) each carry the corner bit that touches it.
        var touching = new[] { new Vector2I(1, 1), new Vector2I(2, 1), new Vector2I(1, 2), new Vector2I(2, 2) };
        var bitmasks = touching.Select(position => overlays[position].Bitmask).OrderBy(mask => mask).ToArray();
        AssertThat(bitmasks.SequenceEqual(new[] { 1, 2, 4, 8 })).IsTrue();
        foreach (var position in touching)
            AssertString(overlays[position].TopTileId).IsEqual(autoTileId);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void AllBitmasksStayInCorner16Range()
    {
        var tileRegistry = new TileRegistry(new GodotAssetPaths(), new CompiledAtlasSource());
        var biomeRegistry = new BiomeRegistry();
        biomeRegistry.RegisterDefaultBiomes(new GodotAssetPaths());
        var rng = RandomSources.Create(4242);
        var gradient = Gradients.FromCards(MapCards(), rng);
        var biomeProvider = BiomeMaps.Create(biomeRegistry.ToBiomeSet(), gradient.GetSignatureAt, GridSize);
        var transitionPairs = new CompiledTransitionResolver(new GodotAssetPaths()).GetAllTransitionPairs().ToList();
        var wfcGenerator = new WfcMapGenerator(
            new WfcAdjacencyRules(transitionPairs),
            TileCatalogData.From(tileRegistry));
        var generator = GridMapGenerator.CreatePipeline(
            rng, biomeProvider, TileCatalogData.From(tileRegistry), wfcGenerator, biomeRegistry.ToBiomeSet(), gradient);
        var map = generator.GenerateMap(GridSize);

        var builder = new DualGridOverlayBuilder(
            tileRegistry,
            DualGridOverlayBuilder.ResolveDefaultPassableTileId(tileRegistry));
        var overlays = builder.Build(map.BackgroundLayer!, map.ForegroundLayer!, map.Size);

        AssertThat(overlays.Count).IsEqual((GridSize.X + 1) * (GridSize.Y + 1));
        foreach (var (position, overlay) in overlays)
        {
            AssertThat(overlay.Bitmask >= 0 && overlay.Bitmask <= 15).IsTrue();
            AssertThat(position.X >= 0 && position.X <= GridSize.X).IsTrue();
            AssertThat(position.Y >= 0 && position.Y <= GridSize.Y).IsTrue();
        }
    }

    private static string[,] Filled(int width, int height, string value)
    {
        var grid = new string[height, width];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
            grid[y, x] = value;
        return grid;
    }

    private static CardSignature[] MapCards() => new[]
    {
        new CardSignature(new[] { 1f, 1f, 0f, -1f, 0f, 0.5f, 0f, 0f }),
        new CardSignature(new[] { -1f, -1f, 1f, 1f, 0f, -0.5f, 0f, 0f })
    };
}
