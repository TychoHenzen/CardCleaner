using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Tests.Features.Deckbuilder.Services.GridMapGeneratorScenarios;

/// <summary>
/// GridMapGenerator through IMapGenerator: it reports progress while it generates, and a cancelled token stops it
/// with MapGenerationCancelledException, which the session treats as an abandoned generation.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class GridMapGeneratorTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static async Task GenerationReportsRisingProgressAndReturnsAGridMap()
    {
        var progress = new RecordingProgress();

        var map = await CreateGenerator().GenerateAsync(Config(), progress);

        var reports = progress.Snapshot();
        AssertInt(reports.Count).IsGreater(0);
        for (var i = 1; i < reports.Count; i++)
            AssertFloat(reports[i]).IsGreaterEqual(reports[i - 1]);
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

    private static GridMapGenerator CreateGenerator()
    {
        var tileRegistry = new TileRegistry();
        var biomeRegistry = new BiomeRegistry();
        biomeRegistry.RegisterDefaultBiomes();
        return new GridMapGenerator(tileRegistry, tileRegistry, biomeRegistry);
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
