using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using CardCleaner.Scripts.Core.Randomness;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Components;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using Godot;

namespace CardCleaner.Tests.Features.Deckbuilder.Components;

/// <summary>
/// Pins the cells that WorldMapTerrainRenderer draws into its four layers for one seeded grid map.
/// The map is built as WfcGridFingerprintTest builds it. A change to the rendering path must keep these cells,
/// their source ids, atlas coordinates and alternative tiles exactly.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class WorldMapTerrainRendererPinTest
{
    private const string Golden = "b3fe408c262e73d2ea60fab1a30eecfaed49a928b7f9247550393ef95f102baf";
    private const ulong GridSeed = 4242;
    private static readonly Vector2I GridSize = new(16, 12);

    [TestCase]
    [TestCategory("Unit")]
    public static void SeededMapRendersTheSameCellsIntoAllFourLayers()
    {
        var tileRegistry = new TileRegistry();
        var biomeRegistry = new BiomeRegistry();
        biomeRegistry.RegisterDefaultBiomes(new GodotAssetPaths());
        var rng = RandomSources.Create(GridSeed);
        var gradient = new CardBasedGradient(MapCards(), rng);
        var biomeProvider = new BiomeMapGenerator(biomeRegistry, gradient.GetSignatureAt, GridSize);
        var transitionPairs = new CompiledTransitionResolver().GetAllTransitionPairs().ToList();
        var wfcGenerator = new WfcMapGenerator(
            new WfcAdjacencyRules(transitionPairs),
            new TileRegistryWfcCatalog(tileRegistry));
        var generator = GridMapGenerator.CreatePipeline(
            rng, biomeProvider, tileRegistry, tileRegistry, wfcGenerator, biomeRegistry, gradient);
        var map = generator.GenerateMap(GridSize);

        var terrain = new TileMapLayer();
        var decoration = new TileMapLayer();
        var structure = new TileMapLayer();
        var effect = new TileMapLayer();
        try
        {
            var tileSet = CompiledAtlasLoader.LoadCompiledTileSet();
            terrain.TileSet = tileSet;
            decoration.TileSet = tileSet;
            structure.TileSet = tileSet;
            effect.TileSet = tileSet;

            var renderer = new WorldMapTerrainRenderer(
                terrain, decoration, structure, effect, tileRegistry, tileRegistry.UsingCompiledAtlas);
            renderer.RenderTerrainTransitions(map);
            renderer.RenderNonTerrainTiles(map);

            var layers = new[] { terrain, decoration, structure, effect };
            // ASSUMPTION: the seeded map draws at least one terrain cell, so an empty terrain layer means the
            // render path did nothing rather than matched the golden.
            AssertThat(terrain.GetUsedCells().Count).IsGreater(0);

            var hash = Fingerprint(layers);
            GD.Print("[WorldMapTerrainRendererPin] hash " + hash);
            AssertString(hash).IsEqual(Golden);
        }
        finally
        {
            terrain.Free();
            decoration.Free();
            structure.Free();
            effect.Free();
        }
    }

    private static CardSignature[] MapCards() => new[]
    {
        new CardSignature(new[] { 1f, 1f, 0f, -1f, 0f, 0.5f, 0f, 0f }),
        new CardSignature(new[] { -1f, -1f, 1f, 1f, 0f, -0.5f, 0f, 0f })
    };

    private static string Fingerprint(TileMapLayer[] layers)
    {
        var text = new StringBuilder();
        for (var index = 0; index < layers.Length; index++)
        {
            var layer = layers[index];
            var cells = layer.GetUsedCells()
                .OrderBy(cell => cell.Y)
                .ThenBy(cell => cell.X)
                .ToList();
            foreach (var cell in cells)
            {
                var source = layer.GetCellSourceId(cell);
                var atlas = layer.GetCellAtlasCoords(cell);
                var alternative = layer.GetCellAlternativeTile(cell);
                text.Append(FormattableString.Invariant($"{index}:{cell.X},{cell.Y}:{source}:{atlas}:{alternative}\n"));
            }
        }

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString()));
        return Convert.ToHexString(digest).ToLowerInvariant();
    }
}
