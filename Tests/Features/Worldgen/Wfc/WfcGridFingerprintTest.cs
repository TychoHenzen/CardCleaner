using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using CardCleaner.Scripts.Core.Randomness;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.Wfc;

/// <summary>
/// Pins the Deckbuilder grid map that GridMapGenerator produces for one fixed seed and two fixed cards.
/// The map is built in the same order GridMapGenerator.GenerateAsync uses, with one shared RNG, minus the
/// async wrapper and its progress profiler. A refactor of the grid WFC path must reproduce this fingerprint
/// exactly. The golden value was recorded on master 17e3c3f, before any refactor, and recorded again in #256 when
/// the start and the enemies moved to MapPopulation (drawn after the terrain from the same RNG, so only the start
/// and enemies lines changed; GridMapGolden.txt shows the same for its map). It was recorded a third time when
/// nearer cells stopped supplying a third enemy: no cell of this 16 x 12 map is 12.5 cells from its start, so it now
/// gets 2 enemies.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class WfcGridFingerprintTest
{
    private const string GridGolden = "0e3c45b8f467eab95e06d133d0cb5bbd654a6cb1df9d5614ba3f81dbacc3d14c";
    private const ulong GridSeed = 4242;
    private static readonly Vector2I GridSize = new(16, 12);

    [TestCase]
    [TestCategory("Unit")]
    public static void DefaultSessionGridPathMatchesMasterFingerprint()
    {
        var tileRegistry = new TileRegistry(new GodotAssetPaths(), new CompiledAtlasSource());
        var biomeRegistry = new BiomeRegistry();
        biomeRegistry.RegisterDefaultBiomes(new GodotAssetPaths());
        var rng = RandomSources.Create(GridSeed);
        var gradient = Gradients.FromCards(MapCards(), rng);
        var biomeProvider = BiomeMaps.Create(biomeRegistry.ToBiomeSet(), gradient.GetSignatureAt, GridSize);
        var transitionPairs = new CompiledTransitionResolver(new GodotAssetPaths()).GetAllTransitionPairs().ToList();
        var wfcGenerator = new WfcMapGenerator(
            new WfcAdjacencyRules(transitionPairs),
            TileCatalogData.From(tileRegistry));
        var generator = GridMapGenerator.CreatePipeline(
            rng, biomeProvider, TileCatalogData.From(tileRegistry), wfcGenerator, biomeRegistry.ToBiomeSet(), gradient);

        // ASSUMPTION: the progress and cancellation profiler that GridMapGenerator.GenerateAsync installs
        // does not change the map, so the pin runs GenerateMap with the default profiler.
        var map = generator.GenerateMap(GridSize);

        // ASSUMPTION: the default biomes load in the test process, so the closest-biome lookup never
        // reaches the fallback biome, whose tile pools are empty because no ITileMetadataProvider is passed.
        AssertThat(biomeRegistry.Count).IsGreater(0);
        // ASSUMPTION: a real background WFC run on seed 4242 yields several tile ids; a failed phase writes one.
        AssertThat(Cells(map.BackgroundLayer).Where(id => !string.IsNullOrEmpty(id)).Distinct().Count())
            .IsGreater(1);
        // ASSUMPTION: a real foreground WFC run places at least one auto-tile; a failed phase leaves it empty.
        AssertBool(Cells(map.ForegroundLayer).Any(id => !string.IsNullOrEmpty(id))).IsTrue();

        var hash = Fingerprint(map);
        GD.Print($"[WfcGridFingerprint] grid seed {GridSeed} hash {hash}");
        AssertString(hash).IsEqual(GridGolden);
    }

    private static CardSignature[] MapCards() => new[]
    {
        new CardSignature(new[] { 1f, 1f, 0f, -1f, 0f, 0.5f, 0f, 0f }),
        new CardSignature(new[] { -1f, -1f, 1f, 1f, 0f, -0.5f, 0f, 0f })
    };

    private static IEnumerable<string> Cells(string[,]? layer) =>
        layer == null ? Array.Empty<string>() : layer.Cast<string>();

    private static string Fingerprint(SimpleMapData map)
    {
        var text = new StringBuilder();
        text.Append(FormattableString.Invariant($"size {map.Size.X}x{map.Size.Y}\n"));
        AppendLayer(text, "tiles", map.TileIds);
        AppendLayer(text, "background", map.BackgroundLayer);
        AppendLayer(text, "foreground", map.ForegroundLayer);
        text.Append(FormattableString.Invariant($"start {map.PlayerStart.X},{map.PlayerStart.Y}\n"));
        AppendPositions(text, "passable", map.PassableTiles);
        AppendPositions(text, "enemies", map.EnemyPositions);

        var digest = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString()));
        return Convert.ToHexString(digest).ToLowerInvariant();
    }

    private static void AppendLayer(StringBuilder text, string name, string[,]? layer)
    {
        if (layer == null)
        {
            text.Append(FormattableString.Invariant($"{name} null\n"));
            return;
        }

        text.Append(FormattableString.Invariant($"{name} {layer.GetLength(1)}x{layer.GetLength(0)}\n"));
        for (var y = 0; y < layer.GetLength(0); y++)
        {
            for (var x = 0; x < layer.GetLength(1); x++)
            {
                var tileId = layer[y, x] ?? "-";
                text.Append(FormattableString.Invariant($"{x},{y}:{tileId}\n"));
            }
        }
    }

    private static void AppendPositions(StringBuilder text, string name, IReadOnlyList<Vector2I> positions)
    {
        text.Append(FormattableString.Invariant($"{name} {positions.Count}\n"));
        for (var index = 0; index < positions.Count; index++)
        {
            text.Append(FormattableString.Invariant($"{index}:{positions[index].X},{positions[index].Y}\n"));
        }
    }
}
