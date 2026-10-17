using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services.GridMapGeneration;

internal sealed class GridMapGenerationPipeline
{
    internal const string VariantSelectionScope = "VariantSelection";
    internal const string BiomeMapBuildScope = "BiomeMapBuild";
    internal const string TwoPhaseWfcScope = "TwoPhaseWfcGeneration";
    internal const string PassableTilesScope = "PassableTileCollection";

    /// <summary>
    /// Each profiler scope GenerateMap opens, with its share of a whole generation's progress. The shares add up to
    /// 1.0, so a generation that finishes has reported 1.0.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, float> ProgressWeights = new Dictionary<string, float>
    {
        [VariantSelectionScope] = 0.05f,
        [BiomeMapBuildScope] = 0.10f,
        [TwoPhaseWfcScope] = 0.80f,
        [PassableTilesScope] = 0.05f
    };

    private readonly GridMapGenerationContext _context;
    private readonly GridMapWfcGenerator _wfcGenerator;

    public GridMapGenerationPipeline(GridMapGenerationContext context)
    {
        _context = context;
        _wfcGenerator = new GridMapWfcGenerator(context);
    }

    public int MaxWfcRetries
    {
        get => _context.MaxWfcRetries;
        set => _context.MaxWfcRetries = value;
    }

    public void SetProfiler(IProfiler profiler)
    {
        _context.Profiler = profiler;
    }

    /// <exception cref="OperationCanceledException">Thrown when the token is cancelled during a WFC solve.</exception>
    public SimpleMapData GenerateMap(Vector2I size, CancellationToken cancellationToken = default)
    {
        ILog.Print($"Generating WFC-based map {size.X}x{size.Y}");

        var variants = SelectVariants();
        var biomeMap = BuildBiomeMap(size);
        var twoPhaseResult = GenerateTwoPhaseWfc(size, biomeMap, cancellationToken);
        var passableTiles = CollectPassableTiles(size, twoPhaseResult.MergedGrid);
        var finalTileIds = CopyGrid(twoPhaseResult.MergedGrid);
        var contextualVariants = new Dictionary<Vector2I, int>();
        var map = new SimpleMapData
        {
            TileIds = finalTileIds,
            BackgroundLayer = twoPhaseResult.BackgroundLayer,
            ForegroundLayer = twoPhaseResult.ForegroundLayer,
            BiomeMap = biomeMap,
            Size = size,
            PassableTiles = passableTiles,
            PerGenerationVariants = variants.PerGenerationVariants,
            PerGenerationGroupVariants = variants.PerGenerationGroupVariants,
            ContextualVariants = contextualVariants,
            TileRegistry = _context.TileRegistry
        };

        // The start and the enemies come from the rule both map kinds share, drawn after the terrain from the same
        // RNG, so the terrain of a seed does not change.
        var grid = new RegularGridMapData(map);
        var placement = MapPopulation.Place(grid, _context.Rng);
        map.PlayerStart = grid.CellIdToPosition(placement.PlayerStartCell);
        map.EnemyPositions = placement.EnemyCells.Select(grid.CellIdToPosition).ToList();

        ILog.Print(
            $"Map generated: {passableTiles.Count} passable tiles, player at {map.PlayerStart}, " +
            $"{map.EnemyPositions.Count} enemies");
        GridMapDiagnostics.ValidateSpatialCoherence(finalTileIds, size);

        return map;
    }

    private VariantSelection SelectVariants()
    {
        using (_context.Profiler.BeginScope(VariantSelectionScope))
        {
            var perGenerationVariants = SelectPerGenerationVariants();
            var perGenerationGroupVariants = SelectPerGenerationGroupVariants();
            _context.WfcGenerator?.SetSelectedVariants(perGenerationGroupVariants);
            return new VariantSelection(perGenerationVariants, perGenerationGroupVariants);
        }
    }

    private string[,] BuildBiomeMap(Vector2I size)
    {
        using (_context.Profiler.BeginScope(BiomeMapBuildScope))
        {
            var biomeMap = new string[size.Y, size.X];
            for (var y = 0; y < size.Y; y++)
            for (var x = 0; x < size.X; x++)
                biomeMap[y, x] = _context.BiomeProvider.GetBiomeAt(new Vector2I(x, y)).Id;
            return biomeMap;
        }
    }

    private TwoPhaseWfcResult GenerateTwoPhaseWfc(
        Vector2I size,
        string[,] biomeMap,
        CancellationToken cancellationToken)
    {
        using (_context.Profiler.BeginScope(TwoPhaseWfcScope))
            return _wfcGenerator.Generate(size, cancellationToken);
    }

    private List<Vector2I> CollectPassableTiles(Vector2I size, string[,] terrainGrid)
    {
        using (_context.Profiler.BeginScope(PassableTilesScope))
        {
            var passableTiles = new List<Vector2I>();
            var placedTiles = new Dictionary<Vector2I, string>();
            for (var y = 0; y < size.Y; y++)
            for (var x = 0; x < size.X; x++)
            {
                var position = new Vector2I(x, y);
                var tileId = terrainGrid[y, x];
                placedTiles[position] = tileId;
                if (IsPassableTile(tileId))
                    passableTiles.Add(position);
            }

            if (passableTiles.Count == 0)
            {
                var center = new Vector2I(size.X / 2, size.Y / 2);
                terrainGrid[center.Y, center.X] = _context.DefaultPassableTileId;
                passableTiles.Add(center);
            }

            return passableTiles;
        }
    }

    private bool IsPassableTile(string tileId)
    {
        var tile = _context.TileRegistry.GetTile(tileId);
        return tile?.IsPassable ?? false;
    }

    private Dictionary<string, int> SelectPerGenerationVariants()
    {
        var variants = new Dictionary<string, int>();

        foreach (var tile in _context.TileRegistry.GetAllTiles())
        {
            if (tile.VariationMode == VariationMode.PerGeneration && tile.HasVariations)
            {
                var variantIndex = _context.Rng.RandiRange(0, tile.Variations!.Count - 1);
                variants[tile.Id] = variantIndex;
            }
        }

        if (variants.Count > 0)
            ILog.Print($"Selected per-generation variants for {variants.Count} tile types");

        return variants;
    }

    private Dictionary<string, string> SelectPerGenerationGroupVariants()
    {
        var selected = new Dictionary<string, string>();

        foreach (var group in _context.TileRegistry.GetAllVariationGroups())
        {
            if (group.Mode != VariationMode.PerGeneration)
                continue;

            var selectedTileId = _context.TileRegistry.SelectPerMapVariant(
                group.BaseName,
                _context.Rng);
            if (selectedTileId != null)
            {
                selected[group.BaseName] = selectedTileId;
                ILog.Print(
                    $"[GridMapGenerator] Selected '{selectedTileId}' for variation group " +
                    $"'{group.BaseName}'");
            }
        }

        return selected;
    }

    private static string[,] CopyGrid(string[,] source)
    {
        var copy = new string[source.GetLength(0), source.GetLength(1)];
        Array.Copy(source, copy, source.Length);
        return copy;
    }

}
