using System.Collections.Generic;
using System.Linq;
using CardCleaner.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services.TilesetLoading;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using Godot;

namespace CardCleaner.Scripts.Core.Services;

public class TileRegistry : ITileRegistry, ITileMetadataProvider
{
    public const int DefaultSourceId = 4;
    private const string DefaultTilesetPath = "res://Assets/Terrain/TileSets/ByPack/FantasyDreamland.tres";

    private readonly Dictionary<string, TileDefinition> _tiles = new();
    private readonly VariationGroupCollection _variationGroups = new();

    /// <summary>
    /// Path to the tileset resource. When using compiled atlas, this returns
    /// a special marker that indicates runtime tileset creation is needed.
    /// </summary>
    public string TilesetPath { get; private set; } = DefaultTilesetPath;

    /// <summary>
    /// Whether the registry is using a compiled atlas instead of source textures.
    /// </summary>
    public bool UsingCompiledAtlas { get; private set; }

    /// <summary>
    /// The compiled TileSet if using atlas mode, null otherwise.
    /// </summary>
    public TileSet? CompiledTileSet { get; private set; }

    /// <summary>
    /// Configuration for tileset-level spatial properties (tile size, grid offset).
    /// </summary>
    public TilesetConfig TilesetConfig { get; private set; } = TilesetConfig.Default;

    /// <summary>
    /// Atlas coordinate mapping for translating original coords to compiled coords.
    /// </summary>
    private CompiledAtlasLoader.AtlasMappingData? _atlasMapping;

    public TileRegistry()
    {
        LoadFromData();
    }

    public void RegisterTile(TileDefinition tile) => _tiles[tile.Id] = tile;

    public TileDefinition? GetTile(string id) => _tiles.GetValueOrDefault(id);

    public IEnumerable<TileDefinition> GetAllTiles() => _tiles.Values;

    public IEnumerable<TileDefinition> GetTilesByBiome(string biomeId)
    {
        foreach (var tile in _tiles.Values)
            if (tile.IsAllowedInBiome(biomeId))
                yield return tile;
    }

    public void Clear()
    {
        _tiles.Clear();
        _variationGroups.Clear();
    }

    /// <summary>
    /// Gets the variation group containing the specified tile, if any.
    /// </summary>
    public VariationGroup? GetVariationGroup(string tileId)
    {
        return _variationGroups.FindGroupContaining(tileId);
    }

    /// <summary>
    /// Gets the variation group by its base name (e.g., "grass").
    /// </summary>
    public VariationGroup? GetVariationGroupByBaseName(string baseName)
    {
        return _variationGroups.GetGroupByBaseName(baseName);
    }

    /// <inheritdoc />
    public bool AreSameTerrainType(string? tileId1, string? tileId2)
    {
        // Both null or empty = same (both empty)
        if (string.IsNullOrEmpty(tileId1) && string.IsNullOrEmpty(tileId2))
            return true;

        // One null/empty, other not = different
        if (string.IsNullOrEmpty(tileId1) || string.IsNullOrEmpty(tileId2))
            return false;

        // Exact match
        if (tileId1 == tileId2)
            return true;

        // Check if both tiles are in the same variation group
        var group1 = _variationGroups.FindGroupContaining(tileId1);
        var group2 = _variationGroups.FindGroupContaining(tileId2);

        if (group1 != null && group2 != null)
            return group1.BaseName == group2.BaseName;

        // Not in variation groups - compare by tile definition's auto-tile equivalence
        // Two auto-tiles with the same variants array pointer are equivalent
        var tile1 = GetTile(tileId1);
        var tile2 = GetTile(tileId2);

        if (tile1?.AutoTileVariants != null && tile2?.AutoTileVariants != null)
            return ReferenceEquals(tile1.AutoTileVariants, tile2.AutoTileVariants);

        return false;
    }

    /// <summary>
    /// Selects a per-map variant for the given base name using weighted random selection.
    /// Returns the tile ID of the selected variant.
    /// </summary>
    public string? SelectPerMapVariant(string baseName, RandomNumberGenerator rng)
    {
        var group = _variationGroups.GetGroupByBaseName(baseName);
        if (group == null || group.Variants.Count == 0)
            return null;

        // Weighted random selection
        var totalWeight = 0f;
        foreach (var v in group.Variants)
            totalWeight += v.Weight;

        if (totalWeight <= 0)
            return group.Variants[0].TileId;

        var roll = rng.Randf() * totalWeight;
        var cumulative = 0f;
        foreach (var v in group.Variants)
        {
            cumulative += v.Weight;
            if (roll <= cumulative)
                return v.TileId;
        }

        return group.Variants[^1].TileId;
    }

    /// <summary>
    /// Gets all variants for a base name with their weights (for per-instance selection).
    /// </summary>
    public IReadOnlyList<VariantWeight> GetInstanceVariants(string baseName)
    {
        return _variationGroups.GetVariantsFor(baseName);
    }

    /// <summary>
    /// Returns all variation groups.
    /// </summary>
    public IEnumerable<VariationGroup> GetAllVariationGroups()
    {
        return _variationGroups.GetAllGroups();
    }

    /// <summary>
    /// Loads tiles from Data/Tiles/tiles.json.
    /// Automatically uses compiled atlas if available for optimized runtime loading.
    /// </summary>
    public void LoadFromData(string? path = null)
    {
        var result = TileDataLoader.LoadTileRegistry(path);

        // Check if compiled atlas is available
        var isAvailable = CompiledAtlasLoader.IsCompiledAtlasAvailable();
        ILog.Print($"[TileRegistry] Compiled atlas available: {isAvailable}");

        if (isAvailable)
        {
            _atlasMapping = CompiledAtlasLoader.LoadMapping();
            ILog.Print($"[TileRegistry] Atlas mapping loaded: {(_atlasMapping != null ? "YES" : "NULL")}");

            CompiledTileSet = CompiledAtlasLoader.LoadCompiledTileSet();
            ILog.Print($"[TileRegistry] Compiled TileSet loaded: {(CompiledTileSet != null ? "YES" : "NULL")}");

            if (CompiledTileSet != null && _atlasMapping != null)
            {
                // Use compiled atlas mode - transition_map.json has all auto-tile transitions
                // Even if some base tiles can't be translated, auto-tiles will work
                UsingCompiledAtlas = true;
                TilesetPath = _atlasMapping.Atlas?.Path ?? result.TilesetPath;

                // CRITICAL: Use compiled atlas tile size, NOT the TSX source tile size
                // TSX may have smaller source tiles (e.g., 8x8) that get composited to larger tiles (e.g., 16x16)
                var atlasTileSize = _atlasMapping.Atlas?.TileSize ?? 16;
                TilesetConfig = new TilesetConfig
                {
                    BaseTileSize = new Vector2I(atlasTileSize, atlasTileSize)
                };

                // Register tiles WITH translation - tiles not in atlas_mapping keep original coords
                // but auto-tiles use transition_map.json which IS complete
                foreach (var tile in result.Tiles)
                {
                    var translated = TranslateTileToCompiledAtlas(tile);
                    RegisterTile(translated);
                }

                ILog.Print(
                    $"[TileRegistry] SUCCESS: Registered {_tiles.Count} tiles, " +
                    "using compiled atlas for auto-tiles");
                BuildVariationGroups();
                return;
            }
            else
            {
                ILog.Print("[TileRegistry] FAILED: CompiledTileSet or mapping is null, falling back");
            }
        }

        // Fallback to original tileset
        UsingCompiledAtlas = false;
        CompiledTileSet = null;
        TilesetPath = result.TilesetPath;
        TilesetConfig = result.TilesetConfig;

        foreach (var tile in result.Tiles)
            RegisterTile(tile);

        ILog.Print($"[TileRegistry] FALLBACK: Registered {_tiles.Count} tiles using tileset {TilesetPath}");
        BuildVariationGroups();
    }

    /// <summary>
    /// Builds variation groups from loaded tiles based on naming patterns.
    /// Tiles with numbered suffixes (grass1, grass2) are grouped as PerGeneration.
    /// Tiles with lettered suffixes (flower_a, flower_b) are grouped as PerInstance.
    /// </summary>
    private void BuildVariationGroups()
    {
        _variationGroups.Clear();

        // Group tiles by detected pattern
        var groups = new Dictionary<string, List<(TileDefinition tile, VariationGroupInfo info)>>();

        foreach (var tile in _tiles.Values)
        {
            var info = TiledTilesetLoader.DetectVariationPattern(tile.Id);
            if (info == null)
                continue;

            if (!groups.ContainsKey(info.BaseName))
                groups[info.BaseName] = new List<(TileDefinition, VariationGroupInfo)>();

            groups[info.BaseName].Add((tile, info));
        }

        // Create VariationGroups for groups with 2+ tiles
        foreach (var (baseName, members) in groups)
        {
            if (members.Count < 2)
                continue;

            // Use the mode from the first member (they should all be the same)
            var mode = members[0].info.Mode;

            // Build variants with weights from tile probability
            var variants = members
                .OrderBy(m => m.info.VariantIndex) // Sort by variant index for consistency
                .Select(m => new VariantWeight(m.tile.Id, m.tile.Probability))
                .ToList();

            var group = new VariationGroup(baseName, mode, variants);
            _variationGroups.AddGroup(group);
        }

        if (_variationGroups.Count > 0)
        {
            ILog.Print($"[TileRegistry] Built {_variationGroups.Count} variation groups from tile naming patterns");
        }
    }

    /// <summary>
    /// Translates a tile's coordinates from original to compiled atlas coordinates.
    /// </summary>
    private TileDefinition TranslateTileToCompiledAtlas(TileDefinition original)
    {
        if (_atlasMapping == null)
            return original;

        // Translate base coordinates
        var (newSourceId, newCoords) = CompiledAtlasLoader.TranslateCoordinates(
            original.SourceId,
            original.AtlasCoords,
            _atlasMapping);

        // Translate auto-tile variants if present
        Vector2I?[]? translatedVariants = null;
        if (original.AutoTileVariants != null)
        {
            translatedVariants = new Vector2I?[original.AutoTileVariants.Length];
            for (var i = 0; i < original.AutoTileVariants.Length; i++)
            {
                if (original.AutoTileVariants[i].HasValue)
                {
                    var (_, variantCoords) = CompiledAtlasLoader.TranslateCoordinates(
                        original.SourceId,
                        original.AutoTileVariants[i]!.Value,
                        _atlasMapping);
                    translatedVariants[i] = variantCoords;
                }
            }
        }

        // Translate variations if present
        Vector2I[]? translatedVars = null;
        if (original.Variations != null)
        {
            translatedVars = new Vector2I[original.Variations.Length];
            for (var i = 0; i < original.Variations.Length; i++)
            {
                var (_, varCoords) = CompiledAtlasLoader.TranslateCoordinates(
                    original.SourceId,
                    original.Variations[i],
                    _atlasMapping);
                translatedVars[i] = varCoords;
            }
        }

        // Translate animation frames if present
        TileAnimation? translatedAnimation = null;
        if (original.Animation != null)
        {
            var translatedFrames = new Vector2I[original.Animation.Frames.Length];
            for (var i = 0; i < original.Animation.Frames.Length; i++)
            {
                var (_, frameCoords) = CompiledAtlasLoader.TranslateCoordinates(
                    original.SourceId,
                    original.Animation.Frames[i],
                    _atlasMapping);
                translatedFrames[i] = frameCoords;
            }
            translatedAnimation = new TileAnimation(translatedFrames, original.Animation.FrameDuration);
        }

        // Create new tile definition with translated coordinates
        return new TileDefinition(
            id: original.Id,
            name: original.Name,
            passability: original.Passability,
            atlasCoords: newCoords,
            sourceId: newSourceId,
            layer: original.Layer,
            elevation: original.Elevation,
            isTransparent: original.IsTransparent,
            allowedBiomes: original.AllowedBiomes,
            size: original.Size,
            decorationDensity: original.DecorationDensity,
            autoTileVariants: translatedVariants,
            autoTileFormatName: original.AutoTileFormatName,
            variations: translatedVars,
            variationMode: original.VariationMode,
            animation: translatedAnimation,
            dominance: original.Dominance,
            innerTerrainId: original.InnerTerrainId,
            outerTerrainId: original.OuterTerrainId,
            isGapTile: original.IsGapTile,
            probability: original.Probability);
    }

    /// <summary>
    /// Reloads all tiles from the data file, clearing existing tiles first
    /// </summary>
    public void Reload(string? path = null)
    {
        Clear();
        LoadFromData(path);
    }

    #region ITileMetadataProvider Implementation

    /// <inheritdoc />
    public IReadOnlyList<TileDefinition> GetSimpleTerrainTiles()
    {
        return _tiles.Values
            .Where(t => t.IsSimpleTerrain)
            .ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<TileDefinition> GetAutoTiles()
    {
        return _tiles.Values
            .Where(t => t.IsAutoTile)
            .ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<TileDefinition> GetGapTiles()
    {
        return _tiles.Values
            .Where(t => t.IsGapTile)
            .ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<TileDefinition> GetPassableTerrainTiles()
    {
        return _tiles.Values
            .Where(t => t.Layer == TileLayer.Terrain && t.IsPassable)
            .ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<TileDefinition> GetSolidTerrainTiles()
    {
        return _tiles.Values
            .Where(t => t.Layer == TileLayer.Terrain && t.IsSolid)
            .ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<TileDefinition> GetDecorationTiles()
    {
        return _tiles.Values
            .Where(t => t.IsDecoration)
            .ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<TileDefinition> GetTilesByLayer(TileLayer layer)
    {
        return _tiles.Values
            .Where(t => t.Layer == layer)
            .ToList();
    }

    /// <inheritdoc />
    IReadOnlyList<TileDefinition> ITileMetadataProvider.GetTilesByBiome(string biomeId)
    {
        return _tiles.Values
            .Where(t => t.IsAllowedInBiome(biomeId))
            .ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<TileDefinition> GetBackgroundTerrainTiles()
    {
        // Simple terrain tiles that can serve as backgrounds for auto-tiles
        return _tiles.Values
            .Where(t => t.IsSimpleTerrain && t.IsPassable)
            .ToList();
    }

    /// <inheritdoc />
    public TileDefinition? GetDefaultPassableTile()
    {
        // First try gap tiles (they're specifically meant to be passable fillers)
        var gapTile = _tiles.Values.FirstOrDefault(t => t.IsGapTile && t.IsPassable);
        if (gapTile != null) return gapTile;

        // Then try simple passable terrain
        return _tiles.Values.FirstOrDefault(t => t.IsSimpleTerrain && t.IsPassable)
            ?? _tiles.Values.FirstOrDefault(t => t.Layer == TileLayer.Terrain && t.IsPassable);
    }

    /// <inheritdoc />
    public TileDefinition? GetDefaultSolidTile()
    {
        return _tiles.Values.FirstOrDefault(t => t.Layer == TileLayer.Terrain && t.IsSolid);
    }

    /// <inheritdoc />
    public TileDefinition? GetDefaultGapTile()
    {
        // First try explicit gap tiles
        var gapTile = _tiles.Values.FirstOrDefault(t => t.IsGapTile);
        if (gapTile != null) return gapTile;

        // Fall back to any simple passable terrain tile
        return _tiles.Values.FirstOrDefault(t => t.IsSimpleTerrain && t.IsPassable);
    }

    /// <inheritdoc />
    public bool IsAutoTile(string tileId)
    {
        return _tiles.TryGetValue(tileId, out var tile) && tile.IsAutoTile;
    }

    /// <inheritdoc />
    public bool IsPassable(string tileId)
    {
        return _tiles.TryGetValue(tileId, out var tile) && tile.IsPassable;
    }

    /// <inheritdoc />
    public bool IsSolid(string tileId)
    {
        return _tiles.TryGetValue(tileId, out var tile) && tile.IsSolid;
    }

    /// <inheritdoc />
    public bool IsGapTile(string tileId)
    {
        return _tiles.TryGetValue(tileId, out var tile) && tile.IsGapTile;
    }

    /// <inheritdoc />
    public bool IsTransparent(string tileId)
    {
        return _tiles.TryGetValue(tileId, out var tile) && tile.IsTransparent;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetWfcTileIds()
    {
        return _tiles.Values
            .Where(t => t.Layer == TileLayer.Terrain)
            .Select(t => t.Id)
            .ToList();
    }

    /// <inheritdoc />
    public string? GetDefaultPassableTileId() => GetDefaultPassableTile()?.Id;

    /// <inheritdoc />
    public string? GetDefaultSolidTileId() => GetDefaultSolidTile()?.Id;

    /// <inheritdoc />
    public string? GetDefaultGapTileId() => GetDefaultGapTile()?.Id;

    #endregion
}
