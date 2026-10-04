using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services.SimpleMapGeneratorSupport;

internal sealed class SimpleMapGenerationPipeline
{
    private readonly SimpleMapGenerationContext _context;
    private readonly SimpleMapWfcGenerator _wfcGenerator;
    private readonly SimpleMapTerrainGenerator _terrainGenerator;

    public SimpleMapGenerationPipeline(SimpleMapGenerationContext context)
    {
        _context = context;
        _wfcGenerator = new SimpleMapWfcGenerator(context);
        _terrainGenerator = new SimpleMapTerrainGenerator(context);
    }

    public int MaxWfcRetries
    {
        get => _context.MaxWfcRetries;
        set => _context.MaxWfcRetries = value;
    }

    public void SetProfiler(IProfiler profiler)
    {
        _context.Profiler = profiler;
        _context.WfcGenerator?.SetProfiler(profiler);
    }

    public SimpleMapData GenerateMap(Vector2I size)
    {
        ILog.Print($"Generating WFC-based map {size.X}x{size.Y}");

        var variants = SelectVariants();
        var biomeMap = BuildBiomeMap(size);
        var twoPhaseResult = GenerateTwoPhaseWfc(size, biomeMap);
        var passableTiles = CollectPassableTiles(size, twoPhaseResult.MergedGrid);
        var decorationOverlays = GenerateTerrainTransitions(
            twoPhaseResult.BackgroundLayer,
            twoPhaseResult.ForegroundLayer,
            size);
        var finalTileIds = CopyGrid(twoPhaseResult.MergedGrid);
        var contextualVariants = new Dictionary<Vector2I, int>();
        var shuffledTiles = passableTiles.OrderBy(_ => _context.Rng.Randf()).ToList();
        var playerStart = shuffledTiles[0];
        var enemyCount = _context.Rng.RandiRange(2, Mathf.Min(3, shuffledTiles.Count - 1));
        var enemyPositions = shuffledTiles.Skip(1).Take(enemyCount).ToList();

        ILog.Print(
            $"Map generated: {passableTiles.Count} passable tiles, player at {playerStart}, " +
            $"{enemyCount} enemies");
        SimpleMapDiagnostics.ValidateSpatialCoherence(finalTileIds, size);

        return new SimpleMapData
        {
            TileIds = finalTileIds,
            BackgroundLayer = twoPhaseResult.BackgroundLayer,
            ForegroundLayer = twoPhaseResult.ForegroundLayer,
            BiomeMap = biomeMap,
            Size = size,
            PlayerStart = playerStart,
            EnemyPositions = enemyPositions,
            PassableTiles = passableTiles,
            PerGenerationVariants = variants.PerGenerationVariants,
            PerGenerationGroupVariants = variants.PerGenerationGroupVariants,
            ContextualVariants = contextualVariants,
            DecorationOverlays = decorationOverlays
        };
    }

    private VariantSelection SelectVariants()
    {
        using (_context.Profiler.BeginScope("VariantSelection"))
        {
            var perGenerationVariants = SelectPerGenerationVariants();
            var perGenerationGroupVariants = SelectPerGenerationGroupVariants();
            _context.WfcGenerator?.SetSelectedVariants(perGenerationGroupVariants);
            return new VariantSelection(perGenerationVariants, perGenerationGroupVariants);
        }
    }

    private string[,] BuildBiomeMap(Vector2I size)
    {
        using (_context.Profiler.BeginScope("BiomeMapBuild"))
        {
            var biomeMap = new string[size.Y, size.X];
            for (var y = 0; y < size.Y; y++)
            for (var x = 0; x < size.X; x++)
                biomeMap[y, x] = _context.BiomeProvider.GetBiomeAt(new Vector2I(x, y)).Id;
            return biomeMap;
        }
    }

    private TwoPhaseWfcResult GenerateTwoPhaseWfc(Vector2I size, string[,] biomeMap)
    {
        using (_context.Profiler.BeginScope("TwoPhaseWfcGeneration"))
            return _wfcGenerator.Generate(size);
    }

    private List<Vector2I> CollectPassableTiles(Vector2I size, string[,] terrainGrid)
    {
        using (_context.Profiler.BeginScope("PassableTileCollection"))
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

    private Dictionary<Vector2I, (string BaseTileId, string TopTileId, int Bitmask)>
        GenerateTerrainTransitions(
            string[,] backgroundLayer,
            string[,] foregroundLayer,
            Vector2I size)
    {
        using (_context.Profiler.BeginScope("TerrainTransitions"))
        {
            var overlays = _terrainGenerator.GenerateTerrainTransitions(
                backgroundLayer,
                foregroundLayer,
                size);
            var transitionCount = overlays.Count(kvp => kvp.Value.Bitmask > 0 && kvp.Value.Bitmask < 15);
            ILog.Print(
                $"Dual-grid terrain: {overlays.Count} visual tiles, " +
                $"{transitionCount} transitions");
            SimpleMapDiagnostics.ValidateBitmaskConsistency(overlays, size.X + 1, size.Y + 1);
            return overlays;
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
                var variantIndex = _context.Rng.RandiRange(0, tile.Variations!.Length - 1);
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
                    $"[SimpleMapGenerator] Selected '{selectedTileId}' for variation group " +
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
