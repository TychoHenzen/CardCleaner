using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Randomness;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Services.GridMapGeneration;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using Godot;

namespace CardCleaner.Tests.Features.Deckbuilder.Services.GridMapGeneratorScenarios;

/// <summary>
/// WFC returns its step timings in its results instead of opening profiler scopes, and the grid pipeline records
/// them in its profiler once per WFC phase under the step names WFC used before.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class GridMapWfcTimingsTest
{
    private const ulong Seed = 4242;
    private static readonly Vector2I Size = new(16, 12);

    private static readonly string[] WfcStepNames =
    {
        "Propagation", "TileSelection", "CellCollapse", "MultiBiomeWfcSolve", "MultiBiomeMapDataConversion"
    };

    [TestCase]
    [TestCategory("Unit")]
    public static void PipelineRecordsEachWfcStepOncePerPhaseAndOpensOnlyItsOwnScopes()
    {
        var setup = new WfcSetup();
        var pipeline = GridMapGenerator.CreatePipeline(
            setup.Rng, setup.BiomeProvider, setup.TileRegistry, setup.TileRegistry,
            setup.WfcGenerator, setup.BiomeRegistry, setup.Gradient);
        var profiler = new RecordingProfiler();
        pipeline.SetProfiler(profiler);

        pipeline.GenerateMap(Size);

        foreach (var name in WfcStepNames)
        {
            var durations = profiler.Durations.Where(entry => entry.Name == name).ToList();
            // One background and one foreground WFC phase each record every step once.
            AssertInt(durations.Count).IsEqual(2);
            AssertBool(durations.All(entry => entry.Milliseconds >= 0)).IsTrue();
        }

        AssertInt(profiler.Durations.Count).IsEqual(WfcStepNames.Length * 2);
        AssertThat(profiler.Scopes).ContainsExactly(
            GridMapGenerationPipeline.VariantSelectionScope,
            GridMapGenerationPipeline.BiomeMapBuildScope,
            GridMapGenerationPipeline.TwoPhaseWfcScope,
            GridMapGenerationPipeline.PassableTilesScope);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void SuccessfulGenerationCarriesPositiveStepTimings()
    {
        var setup = new WfcSetup();

        var result = setup.WfcGenerator.GenerateMultiBiome(
            setup.BiomeRegistry, setup.BiomeProvider.GetBiomeAt, Size, Seed, setup.Gradient.GetSignatureAt);

        AssertBool(result.Success).IsTrue();
        var timings = result.Timings;
        AssertBool(timings.Propagation > TimeSpan.Zero).IsTrue();
        AssertBool(timings.TileSelection > TimeSpan.Zero).IsTrue();
        AssertBool(timings.CellCollapse > TimeSpan.Zero).IsTrue();
        AssertBool(timings.MapDataConversion > TimeSpan.Zero).IsTrue();
        // The per-cell steps run inside the solve, so the solve time covers their sum.
        AssertBool(timings.Solve >= timings.Propagation + timings.TileSelection + timings.CellCollapse).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void FailedGenerationCarriesZeroTimings()
    {
        AssertBool(WfcGenerationResult.Failed("no tiles").Timings == WfcStepTimings.Zero).IsTrue();
    }

    /// <summary>The grid WFC setup WfcGridFingerprintTest uses: default biomes, two map cards, a shared RNG.</summary>
    private sealed class WfcSetup
    {
        public TileRegistry TileRegistry { get; } = new(new GodotAssetPaths());
        public BiomeRegistry BiomeRegistry { get; } = new();
        public IRandom Rng { get; } = RandomSources.Create(Seed);
        public CardBasedGradient Gradient { get; }
        public BiomeMapGenerator BiomeProvider { get; }
        public WfcMapGenerator WfcGenerator { get; }

        public WfcSetup()
        {
            BiomeRegistry.RegisterDefaultBiomes(new GodotAssetPaths());
            Gradient = new CardBasedGradient(MapCards(), Rng);
            BiomeProvider = new BiomeMapGenerator(BiomeRegistry, Gradient.GetSignatureAt, Size);
            var transitionPairs =
                new CompiledTransitionResolver(new GodotAssetPaths()).GetAllTransitionPairs().ToList();
            WfcGenerator = new WfcMapGenerator(
                new WfcAdjacencyRules(transitionPairs),
                new TileRegistryWfcCatalog(TileRegistry));
        }

        private static CardSignature[] MapCards() => new[]
        {
            new CardSignature(new[] { 1f, 1f, 0f, -1f, 0f, 0.5f, 0f, 0f }),
            new CardSignature(new[] { -1f, -1f, 1f, 1f, 0f, -0.5f, 0f, 0f })
        };
    }

    /// <summary>Records every scope name and every recorded duration; scopes do nothing when disposed.</summary>
    private sealed class RecordingProfiler : IProfiler
    {
        public List<string> Scopes { get; } = new();
        public List<(string Name, long Milliseconds)> Durations { get; } = new();

        public IDisposable BeginScope(string name)
        {
            Scopes.Add(name);
            return new NoOpScope();
        }

        public void RecordDuration(string name, long milliseconds) => Durations.Add((name, milliseconds));

        public Dictionary<string, ProfileStats> GetStatistics() => new();

        public void Clear()
        {
            Scopes.Clear();
            Durations.Clear();
        }

        private sealed class NoOpScope : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
