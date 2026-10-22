using CardCleaner.Scripts.Core.Services.CompiledAtlas;
using CardCleaner.Tests.TestUtilities;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Services.GridMapGeneration;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Tests.Features.Deckbuilder.Services.GridMapGeneratorScenarios;

/// <summary>
/// GridMapGenerator through IMapGenerator: it reports rising progress up to 1.0 while it generates, and a cancelled
/// token stops it with MapGenerationCancelledException, which the session treats as an abandoned generation.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class GridMapGeneratorTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static async Task GenerationReportsProgressRisingToCompleteAndReturnsAGridMap()
    {
        var progress = new RecordingProgress();

        var map = await CreateGenerator().GenerateAsync(Config(), progress);

        var reports = progress.Snapshot();
        AssertInt(reports.Count).IsEqual(GridMapGenerationPipeline.ProgressWeights.Count);
        for (var i = 1; i < reports.Count; i++)
            AssertFloat(reports[i]).IsGreater(reports[i - 1]);
        AssertFloat(reports[^1]).IsBetween(0.999f, 1.0f);
        AssertBool(map is SimpleGeneratedMap).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static async Task CancelledTokenStopsTheGeneration()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Exception? caught = null;

        try
        {
            await CreateGenerator().GenerateAsync(Config(), null, cancellation.Token);
        }
        catch (Exception ex)
        {
            caught = ex;
        }

        AssertBool(caught is MapGenerationCancelledException).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static async Task TwoGeneratorsOnOneCatalogAndSeedMakeEqualMaps()
    {
        var tiles = TileCatalogData.From(TileCatalogLoad.Load(new GodotAssetPaths(), new CompiledAtlasSource()));

        var first = await CreateGenerator(tiles).GenerateAsync(Config());
        var second = await CreateGenerator(tiles).GenerateAsync(Config());

        AssertThat(TileIdsOf(first)).IsEqual(TileIdsOf(second));
    }

    private static GridMapGenerator CreateGenerator() =>
        CreateGenerator(TileCatalogData.From(TileCatalogLoad.Load(new GodotAssetPaths(), new CompiledAtlasSource())));

    private static GridMapGenerator CreateGenerator(TileCatalogData tiles)
    {
        var biomeRegistry = new BiomeRegistry();
        biomeRegistry.RegisterDefaultBiomes(new GodotAssetPaths());
        return new GridMapGenerator(
            tiles, biomeRegistry.ToBiomeSet(), CompiledTransitionPairs.Load(), new Wfc.MapGeneratorFactory());
    }

    /// <summary>Flattens a generated grid map to its size, start cell and every tile id, row by row.</summary>
    private static string TileIdsOf(IGeneratedMap generated)
    {
        var map = (SimpleMapData)((SimpleGeneratedMap)generated).RawMapData;
        var text = new System.Text.StringBuilder();
        text.Append(map.Size.X).Append('x').Append(map.Size.Y).Append(" start ")
            .Append(map.PlayerStart.X).Append(',').Append(map.PlayerStart.Y).Append('\n');
        for (var y = 0; y < map.Size.Y; y++)
        {
            for (var x = 0; x < map.Size.X; x++)
                text.Append(map.TileIds[y, x] ?? "-").Append(',');
            text.Append('\n');
        }

        return text.ToString();
    }

    private static MapGenerationConfig Config() => new()
    {
        Size = new Vector2I(12, 12),
        Seed = 4242,
        MapSeeds = new[] { new CardSignature(new[] { 0.3f, -0.2f, 0f, 0f, 0f, 0f, 0f, 0f }) }
    };

    /// <summary>Records reports synchronously; the generator reports from a worker thread.</summary>
    private sealed class RecordingProgress : IProgress<float>
    {
        private readonly List<float> _reports = new();

        public void Report(float value)
        {
            lock (_reports) _reports.Add(value);
        }

        public List<float> Snapshot()
        {
            lock (_reports) return new List<float>(_reports);
        }
    }
}
