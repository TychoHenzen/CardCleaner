using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh.MeshTerrain;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using Godot;
using IrregularMeshNs = CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

namespace CardCleaner.Tests.Features.Worldgen.IrregularMesh.MeshTerrain;

/// <summary>
/// Pins what mesh terrain does when a WFC solve fails: a failed foreground solve clears the foreground and keeps
/// the backgrounds, an empty candidate set never reaches the solver, and a failed rule solve applies the fallback.
/// The stub solver delegates background generation and rule ids to a real solver and fails both solve methods.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class MeshTerrainFailurePathTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void FailedForegroundSolveClearsForegroundAndKeepsBackgrounds()
    {
        // ASSUMPTION: seed 4242 with these cards reaches the foreground step, as MeshTerrainFingerprintTest pins.
        var registry = new TileRegistry();
        var solver = new FailingSolver(
            IWfcTerrainSolver.Create(TransitionPairs(), null, new TileRegistryWfcCatalog(registry)),
            AutoTileId(registry));
        var generator = new IrregularMeshNs.MeshTerrainGenerator(
            new MeshTerrainWfcSetup(solver, registry),
            new Dictionary<string, int>());
        generator.SetBiomeRegistry(DefaultBiomes());

        var mesh = generator.GenerateWithCards(3, TerrainCards(), 4242, relaxationIterations: 8);

        AssertThat(solver.CatalogSolveCalls).IsEqual(1);
        AssertBool(mesh.Vertices.All(vertex => vertex.ForegroundTileId == null)).IsTrue();
        AssertBool(mesh.Vertices.All(vertex => vertex.TileId == null)).IsTrue();
        // ClearForeground writes terrain type 1. A fresh vertex starts at 0, so a skipped clear is visible here.
        AssertBool(mesh.Vertices.All(vertex => vertex.TerrainType == 1)).IsTrue();
        AssertBool(mesh.Quads.All(quad => !string.IsNullOrEmpty(quad.BackgroundTileId))).IsTrue();
        // The fallback writes one background id to every quad, so more than one id shows the solve's result was kept.
        AssertThat(mesh.Quads.Select(quad => quad.BackgroundTileId).Distinct().Count()).IsGreater(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void NoForegroundCandidatesNeverCallsTheSolver()
    {
        var registry = new TileRegistry();
        var solver = new FailingSolver(
            IWfcTerrainSolver.Create(TransitionPairs(), null, new TileRegistryWfcCatalog(registry)),
            AutoTileId(registry));
        var mesh = IrregularMeshNs.MeshGenerator.Generate(new IrregularMeshNs.MeshGenerator.GenerationConfig
        {
            Rings = 3,
            Seed = 4242,
            RelaxationIterations = 8
        });

        // With no biome registered, no terrain tile is allowed in any biome, so the candidate set is empty.
        var foreground = new MeshForegroundWfc(new MeshTerrainWfcSetup(solver, registry));
        var generated = foreground.Generate(mesh, new BiomeRegistry(), 4243UL);

        AssertBool(generated).IsFalse();
        AssertThat(solver.CatalogSolveCalls).IsEqual(0);
        AssertBool(mesh.Vertices.All(vertex => vertex.ForegroundTileId == null)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void FailedRuleSolveAppliesFallbackTerrain()
    {
        var adjacency = new Dictionary<string, HashSet<string>>
        {
            { "grass", new HashSet<string> { "grass", "water" } },
            { "water", new HashSet<string> { "grass", "water" } }
        };
        var tileToTerrain = new Dictionary<string, int> { { "grass", 5 }, { "water", 0 } };
        var solver = new FailingSolver(IWfcTerrainSolver.Create(TransitionPairs(), adjacency, null), "grass");
        var generator = new IrregularMeshNs.MeshTerrainGenerator(new MeshTerrainWfcSetup(solver, null), tileToTerrain);

        var mesh = generator.GenerateFromRules(3, 777);

        AssertThat(solver.RuleSolveCalls).IsEqual(1);
        // The dictionary path has no registry, so the fallback tile is "floor". The map does not name it, so its
        // terrain type is 1.
        AssertBool(mesh.Quads.All(quad => quad.BackgroundTileId == "floor")).IsTrue();
        AssertBool(mesh.Vertices.All(vertex => vertex.TerrainType == 1)).IsTrue();
        AssertBool(mesh.Vertices.All(vertex => vertex.TileId == null && vertex.ForegroundTileId == null)).IsTrue();
    }

    private static IReadOnlyList<(string tileA, string tileB)> TransitionPairs() =>
        new CompiledTransitionResolver().GetAllTransitionPairs().ToList();

    private static string AutoTileId(ITileRegistry registry) =>
        registry.GetAllTiles().First(tile => tile.HasAutoTileVariants).Id;

    private static BiomeRegistry DefaultBiomes()
    {
        var biomes = new BiomeRegistry();
        biomes.RegisterDefaultBiomes();
        return biomes;
    }

    private static CardSignature[] TerrainCards() => new[]
    {
        new CardSignature(new[] { 1f, 1f, 0f, -1f, 0f, 0.5f, 0f, 0f }),
        new CardSignature(new[] { -1f, -1f, 1f, 1f, 0f, -0.5f, 0f, 0f })
    };

    /// <summary>
    /// Delegates background generation and rule ids to a real solver. Both solve methods fail and count their calls.
    /// The failed solutions name a tile on every cell, so a branch that ignores Success would assign it.
    /// </summary>
    private sealed class FailingSolver : IWfcTerrainSolver
    {
        private readonly IWfcTerrainSolver _inner;
        private readonly string _partialTileId;

        public FailingSolver(IWfcTerrainSolver inner, string partialTileId)
        {
            _inner = inner;
            _partialTileId = partialTileId;
        }

        public int CatalogSolveCalls { get; private set; }

        public int RuleSolveCalls { get; private set; }

        public IReadOnlyList<string> RuleTileIds => _inner.RuleTileIds;

        public WfcGenerationResult GenerateBackground(
            BiomeRegistry biomes,
            Func<Vector2I, BiomeDefinition> getBiomeAt,
            Vector2I size,
            ulong seed,
            Func<string, bool> tileFilter) =>
            _inner.GenerateBackground(biomes, getBiomeAt, size, seed, tileFilter);

        public WfcGraphSolution SolveGraphWithCatalog(
            int[][] neighbors,
            IReadOnlyCollection<string> initialTiles,
            ulong seed,
            Func<string, bool>? gapTileFilter)
        {
            CatalogSolveCalls++;
            return Failed(neighbors.Length);
        }

        public WfcGraphSolution SolveGraphWithRules(
            int[][] neighbors,
            IReadOnlyCollection<string> initialTiles,
            ulong seed)
        {
            RuleSolveCalls++;
            return Failed(neighbors.Length);
        }

        private WfcGraphSolution Failed(int cellCount)
        {
            var collapsed = Enumerable.Repeat<string?>(_partialTileId, cellCount).ToArray();
            return new WfcGraphSolution(false, 0, "stub failure", collapsed);
        }
    }
}
