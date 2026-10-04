using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Modifiers;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Modifiers.Soft;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.MeshTerrain;

internal sealed class MeshForegroundWfc
{
    private readonly ITileRegistry? _tileRegistry;
    private readonly CompiledTransitionResolver? _transitionResolver;

    public MeshForegroundWfc(
        ITileRegistry? tileRegistry,
        CompiledTransitionResolver? transitionResolver)
    {
        _tileRegistry = tileRegistry;
        _transitionResolver = transitionResolver;
    }

    public bool Generate(IrregularMesh mesh, BiomeRegistry biomeRegistry, ulong seed)
    {
        if (_tileRegistry == null)
        {
            GD.PrintErr("[MeshTerrainGen] Cannot run direct mesh WFC without tile registry");
            return false;
        }

        var initialTiles = MeshForegroundTileCandidates.Collect(_tileRegistry, biomeRegistry);
        if (initialTiles.Count == 0)
        {
            GD.PrintErr("[MeshTerrainGen] No valid tiles for mesh WFC");
            return false;
        }

        GD.Print(
            $"[MeshTerrainGen] Starting direct mesh WFC with {initialTiles.Count} tiles " +
            $"on {mesh.Vertices.Count} vertices");

        var topology = new IrregularMeshWfcTopology(mesh, initialTiles);
        var solver = CreateSolver(mesh);
        var rng = new RandomNumberGenerator { Seed = seed };
        var result = solver.Solve(topology, null, rng);

        if (!result.Success)
        {
            GD.PrintErr($"[MeshTerrainGen] Direct mesh WFC failed: {result.ErrorMessage}");
            return false;
        }

        GD.Print($"[MeshTerrainGen] Direct mesh WFC succeeded in {result.Iterations} iterations");
        AssignTiles(mesh, topology);
        return true;
    }

    private WfcSolver CreateSolver(IrregularMesh mesh)
    {
        var adjacencyRules = _transitionResolver != null
            ? new WfcAdjacencyRules(_transitionResolver)
            : new WfcAdjacencyRules(new CompiledTransitionResolver());
        ConfigureGapTileAdjacencies(adjacencyRules);

        var propagator = new WfcPropagator(adjacencyRules);
        var selector = new WfcTileSelector();
        var blobTracker = new BlobSizeTracker();
        blobTracker.Initialize(mesh.Vertices.Count);
        var spatialCoherence = new SpatialCoherenceConstraint(_tileRegistry!);

        selector.AddConstraint(new DiminishingReturnsSoftModifier(blobTracker));
        selector.AddConstraint(spatialCoherence);
        selector.AddConstraint(new AutoTileGapConstraint(_tileRegistry!));
        selector.AddConstraint(new NoSolidFillConstraint(_tileRegistry!));

        if (_tileRegistry is TileRegistry concreteRegistry)
            selector.AddConstraint(new TileProbabilityConstraint(concreteRegistry));

        var solver = new WfcSolver(
            propagator,
            selector,
            blobTracker,
            spatialCoherence: spatialCoherence,
            tileRegistry: _tileRegistry);
        solver.MaxIterations = mesh.Vertices.Count * 2;
        return solver;
    }

    private void AssignTiles(IrregularMesh mesh, IrregularMeshWfcTopology topology)
    {
        var autoTileCount = 0;

        for (var index = 0; index < mesh.Vertices.Count; index++)
        {
            var tileId = topology.GetCollapsedTileAt(index);
            var vertex = mesh.Vertices[index];

            if (tileId == null)
            {
                ClearVertex(vertex, 1);
                continue;
            }

            var tile = _tileRegistry!.GetTile(tileId);
            if (tile?.HasAutoTileVariants == true)
            {
                vertex.ForegroundTileId = tileId;
                vertex.TileId = tileId;
                autoTileCount++;
            }
            else
            {
                vertex.ForegroundTileId = null;
                vertex.TileId = null;
            }

            vertex.TerrainType = tile?.IsPassable == true ? 1 : 0;
        }

        GD.Print($"[MeshTerrainGen] Assigned {autoTileCount}/{mesh.Vertices.Count} vertices with auto-tiles");
    }

    private static void ClearVertex(MeshVertex vertex, int terrainType)
    {
        vertex.ForegroundTileId = null;
        vertex.TileId = null;
        vertex.TerrainType = terrainType;
    }

    private void ConfigureGapTileAdjacencies(WfcAdjacencyRules adjacencyRules)
    {
        if (_tileRegistry == null)
            return;

        var gapTiles = new List<string>();
        var autoTiles = new List<string>();
        foreach (var tile in _tileRegistry.GetAllTiles().Where(MeshForegroundTileCandidates.IsCandidate))
        {
            (tile.HasAutoTileVariants ? autoTiles : gapTiles).Add(tile.Id);
        }

        if (gapTiles.Count > 0)
        {
            adjacencyRules.AddMutualAdjacencies(gapTiles);
            foreach (var gapTile in gapTiles)
            {
                foreach (var autoTile in autoTiles)
                    adjacencyRules.AddAdjacency(gapTile, autoTile);
            }
        }

        foreach (var autoTile in autoTiles)
            adjacencyRules.EnsureSelfAdjacency(autoTile);
    }
}
