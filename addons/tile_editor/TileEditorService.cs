#if TOOLS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Godot;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// Service for loading, modifying, and saving tile data in the editor
/// </summary>
[Tool]
public partial class TileEditorService : RefCounted
{
    [Signal]
    public delegate void TileAddedEventHandler(string tileId);

    [Signal]
    public delegate void TileModifiedEventHandler(string tileId);

    [Signal]
    public delegate void TileRemovedEventHandler(string tileId);

    [Signal]
    public delegate void TilesLoadedEventHandler();

    // Biome signals
    [Signal]
    public delegate void BiomesLoadedEventHandler();

    [Signal]
    public delegate void BiomeAddedEventHandler(string biomeId);

    [Signal]
    public delegate void BiomeModifiedEventHandler(string biomeId);

    [Signal]
    public delegate void BiomeRemovedEventHandler(string biomeId);

    // Auto-tile format signals
    [Signal]
    public delegate void AutoTileFormatsLoadedEventHandler();

    private const string TilesPath = "res://Data/Tiles/tiles.json";
    private const string DefaultTilesetPath = "res://Assets/Terrain/TileSets/ByPack/FantasyDreamland.tres";
    private static readonly Regex TileIdPattern = new(@"^[a-z][a-z0-9_]*$", RegexOptions.Compiled);

    /// <summary>
    /// Creates fresh read JsonSerializerOptions per call to avoid assembly unload issues.
    /// See: https://github.com/godotengine/godot/issues/78513
    /// </summary>
    private static JsonSerializerOptions CreateJsonOptions() => new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>
    /// Creates fresh write JsonSerializerOptions per call to avoid assembly unload issues.
    /// See: https://github.com/godotengine/godot/issues/78513
    /// </summary>
    private static JsonSerializerOptions CreateWriteOptions() => new()
    {
        WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly Dictionary<string, EditableTile> _tiles = new();
    private readonly Dictionary<string, EditableBiome> _biomes = new();
    private readonly List<EditableAutoTileFormat> _customAutoTileFormats = new();
    private TileSet? _tileSet;
    private string _version = "1.0";

    public int TileCount => _tiles.Count;
    public IEnumerable<EditableTile> AllTiles => _tiles.Values;
    public int BiomeCount => _biomes.Count;
    public IEnumerable<EditableBiome> AllBiomes => _biomes.Values;
    public IEnumerable<EditableAutoTileFormat> CustomAutoTileFormats => _customAutoTileFormats;
    public string TilesetPath { get; private set; } = DefaultTilesetPath;
    public TileSet? TileSet => _tileSet;

    /// <summary>
    /// Updates the custom auto-tile formats from the format editor panel.
    /// </summary>
    public void UpdateCustomAutoTileFormats(IEnumerable<EditableAutoTileFormat> formats)
    {
        _customAutoTileFormats.Clear();
        _customAutoTileFormats.AddRange(formats);
    }

    /// <summary>
    /// Gets all available auto-tile format names from the registry.
    /// Returns names in order: built-in formats first, then custom formats alphabetically.
    /// </summary>
    public IReadOnlyList<string> GetAvailableFormatNames()
    {
        return Features.Worldgen.AutoTiling.AutoTileFormatRegistry.GetAll()
            .OrderBy(f => f.IsBuiltIn ? 0 : 1)
            .ThenBy(f => f.Name)
            .Select(f => f.Name)
            .ToList();
    }

    /// <summary>
    /// Gets a format definition by name from the registry.
    /// Returns null if the format is not found.
    /// </summary>
    public Features.Worldgen.AutoTiling.AutoTileFormatDefinition? GetFormatDefinition(string formatName)
    {
        if (string.IsNullOrEmpty(formatName)) return null;
        return Features.Worldgen.AutoTiling.AutoTileFormatRegistry.TryGet(formatName, out var format)
            ? format
            : null;
    }

    /// <summary>
    /// Checks if a format name refers to a built-in (non-editable) format.
    /// </summary>
    public bool IsBuiltInFormat(string formatName)
    {
        var format = GetFormatDefinition(formatName);
        return format?.IsBuiltIn ?? false;
    }

    /// <summary>
    /// Gets the expected variant count for a format.
    /// Returns 16 for corner/edge formats, 47 for blob format.
    /// </summary>
    public int GetExpectedVariantCount(string formatName)
    {
        var format = GetFormatDefinition(formatName);
        if (format == null)
        {
            // Default to corner16 behavior
            return 16;
        }
        return format.AllowedBitmasks.Count;
    }

    /// <summary>
    /// Gets the display name for a format (name with built-in indicator).
    /// </summary>
    public string GetFormatDisplayName(string formatName)
    {
        var format = GetFormatDefinition(formatName);
        if (format == null) return formatName;
        return format.IsBuiltIn ? $"{format.Name} (built-in)" : format.Name;
    }

    public void LoadTiles()
    {
        _tiles.Clear();
        _biomes.Clear();
        _customAutoTileFormats.Clear();
        Features.Worldgen.AutoTiling.AutoTileFormatRegistry.ClearCustomFormats();

        var absolutePath = ProjectSettings.GlobalizePath(TilesPath);
        if (!File.Exists(absolutePath))
        {
            GD.PrintErr($"[TileEditorService] File not found: {absolutePath}");
            EmitSignal(SignalName.TilesLoaded);
            return;
        }

        try
        {
            var json = File.ReadAllText(absolutePath);
            var data = JsonSerializer.Deserialize<TileRegistryData>(json, CreateJsonOptions());

            if (data?.Tiles == null)
            {
                GD.PrintErr("[TileEditorService] Invalid JSON structure");
                EmitSignal(SignalName.TilesLoaded);
                return;
            }

            _version = data.Version ?? "1.0";
            TilesetPath = data.Tileset ?? DefaultTilesetPath;

            // Load the TileSet resource
            if (ResourceLoader.Exists(TilesetPath))
            {
                _tileSet = ResourceLoader.Load<TileSet>(TilesetPath);
                GD.Print($"[TileEditorService] Loaded TileSet from {TilesetPath}");
            }
            else
            {
                GD.PrintErr($"[TileEditorService] TileSet not found: {TilesetPath}");
            }

            // Load tiles
            foreach (var tileData in data.Tiles)
            {
                if (string.IsNullOrEmpty(tileData.Id)) continue;

                var tile = new EditableTile
                {
                    Id = tileData.Id,
                    Name = tileData.Name ?? "",
                    Passability = tileData.Passability ?? "passable",
                    AtlasX = tileData.AtlasCoords?.X ?? 0,
                    AtlasY = tileData.AtlasCoords?.Y ?? 0,
                    SourceId = tileData.SourceId ?? 4,
                    Layer = tileData.Layer ?? "terrain",
                    Elevation = tileData.Elevation ?? 0f,
                    IsTransparent = tileData.IsTransparent ?? true,
                    Biomes = tileData.Biomes?.ToList() ?? new List<string>(),
                    SizeX = tileData.Size?.X ?? 1,
                    SizeY = tileData.Size?.Y ?? 1,
                    SourceScale = tileData.SourceScale ?? 1.0f
                };

                // Load auto-tile variants if present
                if (tileData.AutoTileVariants != null && tileData.AutoTileVariants.Any(v => v != null))
                {
                    // Determine correct array size based on format (47 for blob47, 16 for corner16/edge16)
                    var variantCount = tileData.AutoTileFormat == "blob47" ? 47 : 16;
                    tile.AutoTileVariants = new Vector2I?[variantCount];
                    for (var i = 0; i < Math.Min(variantCount, tileData.AutoTileVariants.Length); i++)
                    {
                        var v = tileData.AutoTileVariants[i];
                        if (v != null)
                            tile.AutoTileVariants[i] = new Vector2I(v.X, v.Y);
                    }
                }

                // Load decoration density (default 1.0 if not specified)
                if (tileData.DecorationDensity.HasValue)
                    tile.DecorationDensity = Math.Clamp(tileData.DecorationDensity.Value, 0f, 1f);

                // Load auto-tile format
                if (!string.IsNullOrEmpty(tileData.AutoTileFormat))
                    tile.AutoTileFormat = tileData.AutoTileFormat;

                // Load variations if present
                if (tileData.Variations != null && tileData.Variations.Length > 0)
                {
                    tile.Variations = new Vector2I[tileData.Variations.Length];
                    for (var i = 0; i < tileData.Variations.Length; i++)
                    {
                        tile.Variations[i] = new Vector2I(tileData.Variations[i].X, tileData.Variations[i].Y);
                    }
                }

                // Load variation mode
                if (!string.IsNullOrEmpty(tileData.VariationMode))
                    tile.VariationMode = tileData.VariationMode.ToLowerInvariant();

                // Load animation if present
                if (tileData.Animation?.Frames != null && tileData.Animation.Frames.Length > 0)
                {
                    tile.AnimationFrames = new Vector2I[tileData.Animation.Frames.Length];
                    for (var i = 0; i < tileData.Animation.Frames.Length; i++)
                    {
                        tile.AnimationFrames[i] = new Vector2I(
                            tileData.Animation.Frames[i].X,
                            tileData.Animation.Frames[i].Y);
                    }
                    tile.AnimationFrameDuration = tileData.Animation.FrameDuration;
                }

                // Load tile mode (compute from existing data if not present for backwards compatibility)
                if (!string.IsNullOrEmpty(tileData.TileMode))
                    tile.TileMode = tileData.TileMode.ToLowerInvariant();
                else
                    tile.TileMode = ComputeTileModeFromData(tile);

                // Load dominance (null means use file order)
                tile.Dominance = tileData.Dominance;

                // Load terrain transition references for auto-tiles
                tile.InnerTerrainId = tileData.InnerTerrain;
                tile.OuterTerrainId = tileData.OuterTerrain;

                // Load description
                tile.Description = tileData.Description;

                _tiles[tile.Id] = tile;
            }

            GD.Print($"[TileEditorService] Loaded {_tiles.Count} tiles");

            // Load biomes
            if (data.Biomes != null)
            {
                foreach (var (biomeId, biomeData) in data.Biomes)
                {
                    var biome = new EditableBiome
                    {
                        Id = biomeId,
                        DisplayName = biomeData.DisplayName ?? biomeId,
                        Signature = biomeData.Signature ?? new float[8],
                        BlockedPercentage = biomeData.BlockedPercentage,
                        PassableTiles = biomeData.PassableTiles ?? new Dictionary<string, float>(),
                        BlockedTiles = biomeData.BlockedTiles ?? new Dictionary<string, float>()
                    };
                    _biomes[biomeId] = biome;
                }
                GD.Print($"[TileEditorService] Loaded {_biomes.Count} biomes");
            }

            // Load custom auto-tile formats
            if (data.AutoTileFormats != null)
            {
                foreach (var formatData in data.AutoTileFormats)
                {
                    if (string.IsNullOrEmpty(formatData.Name)) continue;

                    var bitmaskType = formatData.BitmaskType?.ToLowerInvariant() switch
                    {
                        "edge4" => Features.Worldgen.AutoTiling.BitmaskType.Edge4,
                        "full8" => Features.Worldgen.AutoTiling.BitmaskType.Full8,
                        _ => Features.Worldgen.AutoTiling.BitmaskType.Corner4
                    };

                    var allowedBitmasks = formatData.AllowedBitmasks?.ToHashSet()
                        ?? Features.Worldgen.AutoTiling.AutoTileFormatDefinition.AllBitmasksFor(bitmaskType);

                    // Parse variant sizes from JSON
                    var variantSizes = new Dictionary<int, Godot.Vector2I>();
                    if (formatData.VariantSizes != null)
                    {
                        foreach (var (bitmaskStr, sizeData) in formatData.VariantSizes)
                        {
                            if (int.TryParse(bitmaskStr, out var bitmask))
                            {
                                variantSizes[bitmask] = new Godot.Vector2I(sizeData.X, sizeData.Y);
                            }
                        }
                    }

                    var editableFormat = new EditableAutoTileFormat
                    {
                        Name = formatData.Name,
                        BitmaskType = bitmaskType,
                        AllowedBitmasks = allowedBitmasks
                    };

                    // Populate editable format's variant mappings with sizes
                    foreach (var bitmask in allowedBitmasks)
                    {
                        var size = variantSizes.TryGetValue(bitmask, out var s) ? s : new Godot.Vector2I(1, 1);
                        editableFormat.VariantMappings[bitmask] = new EditableFormatVariant
                        {
                            SizeX = size.X,
                            SizeY = size.Y,
                            OffsetX = 0,
                            OffsetY = 0
                        };
                    }

                    _customAutoTileFormats.Add(editableFormat);

                    // Register with the global registry
                    var variantMappings = new Dictionary<int, Features.Worldgen.AutoTiling.VariantDefinition>();
                    foreach (var bitmask in allowedBitmasks)
                    {
                        var size = variantSizes.TryGetValue(bitmask, out var s) ? s : new Godot.Vector2I(1, 1);
                        variantMappings[bitmask] = new Features.Worldgen.AutoTiling.VariantDefinition(
                            Godot.Vector2I.Zero,
                            size,
                            Godot.Vector2I.Zero
                        );
                    }

                    var definition = new Features.Worldgen.AutoTiling.AutoTileFormatDefinition(
                        formatData.Name,
                        bitmaskType,
                        allowedBitmasks,
                        variantMappings,
                        isBuiltIn: false
                    );
                    Features.Worldgen.AutoTiling.AutoTileFormatRegistry.Register(definition);
                }
                GD.Print($"[TileEditorService] Loaded {_customAutoTileFormats.Count} custom auto-tile formats");
            }

            EmitSignal(SignalName.TilesLoaded);
            EmitSignal(SignalName.BiomesLoaded);
            EmitSignal(SignalName.AutoTileFormatsLoaded);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[TileEditorService] Error loading tiles: {ex.Message}");
            EmitSignal(SignalName.TilesLoaded);
            EmitSignal(SignalName.BiomesLoaded);
            EmitSignal(SignalName.AutoTileFormatsLoaded);
        }
    }

    /// <summary>
    /// Get the texture for a tile from the TileSet
    /// </summary>
    public Texture2D? GetTileTexture(EditableTile tile)
    {
        if (!IsTileSetValid()) return null;

        try
        {
            var source = _tileSet!.GetSource(tile.SourceId) as TileSetAtlasSource;
            return source?.Texture;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[TileEditorService] Failed to get tile texture: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Check if the TileSet reference is still valid (not freed after a rebuild).
    /// If invalid, automatically attempts to reload the TileSet to restore functionality
    /// after C# assembly reloads.
    /// </summary>
    private bool IsTileSetValid()
    {
        if (_tileSet == null) return TryReloadTileSet();

        try
        {
            // Try to access a property to verify the object is still valid
            if (GodotObject.IsInstanceValid(_tileSet))
                return true;
        }
        catch
        {
            // Object became invalid (assembly reload)
        }

        // Reference is stale - attempt to reload
        _tileSet = null;
        return TryReloadTileSet();
    }

    /// <summary>
    /// Attempts to reload the TileSet resource after it becomes invalid.
    /// This happens after C# assembly reloads when cached references become stale.
    /// </summary>
    private bool TryReloadTileSet()
    {
        if (string.IsNullOrEmpty(TilesetPath)) return false;

        try
        {
            if (ResourceLoader.Exists(TilesetPath))
            {
                _tileSet = ResourceLoader.Load<TileSet>(TilesetPath);
                if (_tileSet != null)
                {
                    GD.Print($"[TileEditorService] Reloaded TileSet after assembly reload from {TilesetPath}");
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[TileEditorService] Failed to reload TileSet: {ex.Message}");
        }

        return false;
    }

    /// <summary>
    /// Get the texture region for a tile (supports multi-tile sizes and source scaling)
    /// </summary>
    public Rect2I GetTileTextureRegion(EditableTile tile)
    {
        if (!IsTileSetValid()) return new Rect2I(0, 0, 16, 16);

        try
        {
            var source = _tileSet!.GetSource(tile.SourceId) as TileSetAtlasSource;
            if (source == null) return new Rect2I(0, 0, 16, 16);

            var baseTileSize = _tileSet.TileSize;
            // Account for source scale: 0.5x = 32px source, 2.0x = 8px source
            var actualTileSize = new Vector2I(
                (int)(baseTileSize.X / tile.SourceScale),
                (int)(baseTileSize.Y / tile.SourceScale)
            );
            var atlasCoords = new Vector2I(tile.AtlasX, tile.AtlasY);

            // Use the tile's size for multi-tile support
            var regionSize = new Vector2I(actualTileSize.X * tile.SizeX, actualTileSize.Y * tile.SizeY);
            return new Rect2I(atlasCoords * actualTileSize, regionSize);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[TileEditorService] Failed to get texture region: {ex.Message}");
            return new Rect2I(0, 0, 16, 16);
        }
    }

    /// <summary>
    /// Get all available TileSetAtlasSource entries with descriptive info
    /// </summary>
    public List<AtlasSourceInfo> GetAvailableAtlasSources()
    {
        var sources = new List<AtlasSourceInfo>();
        if (!IsTileSetValid()) return sources;

        try
        {
            var sourceCount = _tileSet!.GetSourceCount();
            for (int i = 0; i < sourceCount; i++)
            {
                var sourceId = _tileSet.GetSourceId(i);
                var source = _tileSet.GetSource(sourceId) as TileSetAtlasSource;
                if (source == null) continue;

                var textureName = source.Texture?.ResourcePath ?? "Unknown";
                if (textureName.Contains('/'))
                    textureName = textureName.GetFile();

                sources.Add(new AtlasSourceInfo
                {
                    SourceId = sourceId, DisplayName = $"Source {sourceId}: {textureName}", Source = source
                });
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[TileEditorService] Failed to get atlas sources: {ex.Message}");
        }

        return sources;
    }

    /// <summary>
    /// Get a specific TileSetAtlasSource by ID
    /// </summary>
    public TileSetAtlasSource? GetAtlasSource(int sourceId)
    {
        if (!IsTileSetValid()) return null;
        try
        {
            return _tileSet!.GetSource(sourceId) as TileSetAtlasSource;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[TileEditorService] Failed to get atlas source {sourceId}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Get the set of all SourceId values currently referenced by tiles
    /// </summary>
    public HashSet<int> GetUsedSourceIds()
    {
        return _tiles.Values.Select(t => t.SourceId).ToHashSet();
    }

    /// <summary>
    /// Remove a source from the TileSet and save the resource
    /// </summary>
    public bool RemoveSource(int sourceId)
    {
        if (!IsTileSetValid()) return false;

        try
        {
            // Verify no tiles are using this source
            if (_tiles.Values.Any(t => t.SourceId == sourceId))
            {
                GD.PrintErr($"[TileEditorService] Cannot remove source {sourceId}: tiles are using it");
                return false;
            }

            _tileSet!.RemoveSource(sourceId);
            var saveResult = ResourceSaver.Save(_tileSet, TilesetPath);

            if (saveResult == Error.Ok)
            {
                GD.Print($"[TileEditorService] Removed source {sourceId} and saved TileSet");
                return true;
            }

            GD.PrintErr($"[TileEditorService] Failed to save TileSet after removing source: {saveResult}");
            return false;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[TileEditorService] Failed to remove source: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Find duplicate atlas sources by comparing actual texture content.
    /// Returns groups of source IDs that have identical textures.
    /// </summary>
    public List<List<int>> FindDuplicateSources()
    {
        var duplicateGroups = new List<List<int>>();
        if (!IsTileSetValid()) return duplicateGroups;

        try
        {
            var sources = GetAvailableAtlasSources();
            var processed = new HashSet<int>();

            // Build a dictionary of source ID to image data hash
            var sourceImageData = new Dictionary<int, byte[]>();
            foreach (var source in sources)
            {
                if (source.Source?.Texture == null) continue;
                var image = source.Source.Texture.GetImage();
                if (image != null)
                {
                    sourceImageData[source.SourceId] = image.GetData();
                }
            }

            // Find groups with identical content
            foreach (var kvp1 in sourceImageData)
            {
                if (processed.Contains(kvp1.Key)) continue;

                var group = new List<int> { kvp1.Key };

                foreach (var kvp2 in sourceImageData)
                {
                    if (kvp1.Key == kvp2.Key || processed.Contains(kvp2.Key)) continue;

                    // Compare byte arrays
                    if (kvp1.Value.Length == kvp2.Value.Length && kvp1.Value.SequenceEqual(kvp2.Value))
                    {
                        group.Add(kvp2.Key);
                        processed.Add(kvp2.Key);
                    }
                }

                processed.Add(kvp1.Key);

                // Only add if there are duplicates (more than 1 in group)
                if (group.Count > 1)
                {
                    group.Sort(); // Ensure lowest ID is first
                    duplicateGroups.Add(group);
                }
            }

            return duplicateGroups;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[TileEditorService] Failed to find duplicate sources: {ex.Message}");
            return duplicateGroups;
        }
    }

    /// <summary>
    /// Remove duplicate sources, keeping the one with the lowest ID.
    /// Reassigns tiles from higher ID sources to the lowest ID source.
    /// </summary>
    /// <returns>Number of duplicate sources removed</returns>
    public int RemoveDuplicateSources()
    {
        if (!IsTileSetValid()) return 0;

        var duplicateGroups = FindDuplicateSources();
        if (duplicateGroups.Count == 0) return 0;

        var removedCount = 0;

        try
        {
            foreach (var group in duplicateGroups)
            {
                var keepId = group[0]; // Lowest ID (group is sorted)

                // Reassign tiles from duplicate sources to the kept source
                for (var i = 1; i < group.Count; i++)
                {
                    var removeId = group[i];

                    // Update tiles that reference the duplicate source
                    foreach (var tile in _tiles.Values.Where(t => t.SourceId == removeId))
                    {
                        tile.SourceId = keepId;
                    }

                    // Remove the duplicate source from TileSet
                    _tileSet!.RemoveSource(removeId);
                    removedCount++;
                    GD.Print($"[TileEditorService] Removed duplicate source {removeId} (kept {keepId})");
                }
            }

            // Save the TileSet
            var saveResult = ResourceSaver.Save(_tileSet!, TilesetPath);
            if (saveResult != Error.Ok)
            {
                GD.PrintErr($"[TileEditorService] Failed to save TileSet after removing duplicates: {saveResult}");
            }

            return removedCount;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[TileEditorService] Failed to remove duplicate sources: {ex.Message}");
            return removedCount;
        }
    }

    /// <summary>
    /// Check if atlas source IDs are already contiguous (0, 1, 2, ... N-1)
    /// </summary>
    public bool AreSourceIdsContiguous()
    {
        if (!IsTileSetValid()) return true;

        try
        {
            var sourceCount = _tileSet!.GetSourceCount();
            if (sourceCount == 0) return true;

            var sourceIds = new List<int>();
            for (var i = 0; i < sourceCount; i++)
            {
                sourceIds.Add(_tileSet.GetSourceId(i));
            }
            sourceIds.Sort();

            for (var i = 0; i < sourceIds.Count; i++)
            {
                if (sourceIds[i] != i) return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[TileEditorService] Failed to check source IDs: {ex.Message}");
            return true;
        }
    }

    /// <summary>
    /// Compact atlas source IDs to be contiguous (0, 1, 2, ... N-1).
    /// Updates in-memory tile data but does NOT save tiles.json - caller must call SaveTiles().
    /// </summary>
    /// <returns>Dictionary mapping old IDs to new IDs, empty if already contiguous or failed</returns>
    public Dictionary<int, int> CompactAtlasSourceIds()
    {
        var mapping = new Dictionary<int, int>();
        if (!IsTileSetValid())
        {
            GD.PrintErr("[TileEditorService] Cannot compact: TileSet not loaded");
            return mapping;
        }

        try
        {
        var sourceCount = _tileSet!.GetSourceCount();
        if (sourceCount == 0)
        {
            return mapping;
        }

        // Collect and sort current source IDs
        var sourceIds = new List<int>();
        for (var i = 0; i < sourceCount; i++)
        {
            sourceIds.Add(_tileSet.GetSourceId(i));
        }
        sourceIds.Sort();

        // Check if already contiguous
        var isContiguous = true;
        for (var i = 0; i < sourceIds.Count; i++)
        {
            if (sourceIds[i] != i)
            {
                isContiguous = false;
                break;
            }
        }

        if (isContiguous)
        {
            GD.Print("[TileEditorService] Source IDs already contiguous");
            return mapping;
        }

        // Collect sources before removing (references should survive RemoveSource)
        var sources = new List<(int oldId, TileSetAtlasSource source)>();
        foreach (var oldId in sourceIds)
        {
            var source = _tileSet.GetSource(oldId) as TileSetAtlasSource;
            if (source != null)
            {
                sources.Add((oldId, source));
            }
        }

        // Remove all sources
        foreach (var oldId in sourceIds)
        {
            _tileSet.RemoveSource(oldId);
        }

        // Re-add with contiguous IDs
        for (var newId = 0; newId < sources.Count; newId++)
        {
            var (oldId, source) = sources[newId];
            _tileSet.AddSource(source, newId);
            mapping[oldId] = newId;
        }

        // Update in-memory tile data
        foreach (var tile in _tiles.Values)
        {
            if (mapping.TryGetValue(tile.SourceId, out var newId))
            {
                tile.SourceId = newId;
            }
        }

        // Save the TileSet resource
        var saveResult = ResourceSaver.Save(_tileSet, TilesetPath);
        if (saveResult != Error.Ok)
        {
            GD.PrintErr($"[TileEditorService] Failed to save TileSet after compaction: {saveResult}");
        }
        else
        {
            GD.Print($"[TileEditorService] Compacted {sources.Count} sources, remapped {mapping.Count} IDs");
        }

        return mapping;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[TileEditorService] Failed to compact atlas source IDs: {ex.Message}");
            return mapping;
        }
    }

    public EditableTile? GetTile(string id) => _tiles.GetValueOrDefault(id);

    public EditableBiome? GetBiome(string id) => _biomes.GetValueOrDefault(id);

    public void UpdateTile(EditableTile tile)
    {
        if (!_tiles.ContainsKey(tile.Id))
        {
            GD.PrintErr($"[TileEditorService] Tile not found: {tile.Id}");
            return;
        }

        _tiles[tile.Id] = tile;
        EmitSignal(SignalName.TileModified, tile.Id);
    }

    public bool AddTile(EditableTile tile)
    {
        if (_tiles.ContainsKey(tile.Id))
        {
            GD.PrintErr($"[TileEditorService] Tile already exists: {tile.Id}");
            return false;
        }

        _tiles[tile.Id] = tile;
        EmitSignal(SignalName.TileAdded, tile.Id);
        return true;
    }

    public bool RemoveTile(string id)
    {
        if (!_tiles.Remove(id))
        {
            return false;
        }

        EmitSignal(SignalName.TileRemoved, id);
        return true;
    }

    public IEnumerable<EditableTile> GetTilesForBiome(string biome)
    {
        return _tiles.Values.Where(t =>
            t.Biomes.Count == 0 || t.Biomes.Contains(biome, StringComparer.OrdinalIgnoreCase));
    }

    // Biome CRUD operations

    public bool AddBiome(EditableBiome biome)
    {
        if (_biomes.ContainsKey(biome.Id))
        {
            GD.PrintErr($"[TileEditorService] Biome already exists: {biome.Id}");
            return false;
        }

        _biomes[biome.Id] = biome;
        EmitSignal(SignalName.BiomeAdded, biome.Id);
        return true;
    }

    public void UpdateBiome(EditableBiome biome)
    {
        if (!_biomes.ContainsKey(biome.Id))
        {
            GD.PrintErr($"[TileEditorService] Biome not found: {biome.Id}");
            return;
        }

        _biomes[biome.Id] = biome;
        EmitSignal(SignalName.BiomeModified, biome.Id);
    }

    public bool RemoveBiome(string id)
    {
        if (!_biomes.Remove(id))
        {
            return false;
        }

        EmitSignal(SignalName.BiomeRemoved, id);
        return true;
    }

    /// <summary>
    /// Normalize weights so they sum to 1.0
    /// </summary>
    private static Dictionary<string, float> NormalizeWeights(Dictionary<string, float> weights)
    {
        if (weights.Count == 0)
            return weights;

        var sum = weights.Values.Sum();
        if (sum <= 0)
            return weights;

        return weights.ToDictionary(
            kvp => kvp.Key,
            kvp => (float)Math.Round(kvp.Value / sum, 2)
        );
    }

    /// <summary>
    /// Compute tile mode from existing tile data for backwards compatibility.
    /// </summary>
    private static string ComputeTileModeFromData(EditableTile tile)
    {
        // Priority: Animation > AutoTile > Variations > Plain
        if (tile.HasAnimation)
            return "animated";

        if (tile.HasAutoTileVariants)
        {
            // Check if also has variations with per-map mode
            if (tile.HasVariations && tile.VariationMode == "pergeneration")
                return "permapvariationautotile";
            return "autotile";
        }

        if (tile.HasVariations)
        {
            return tile.VariationMode == "pergeneration" ? "permapvariations" : "pertilevariations";
        }

        return "plain";
    }

    public (bool success, string message) ValidateTile(EditableTile tile)
    {
        if (string.IsNullOrWhiteSpace(tile.Id))
            return (false, "ID is required");

        if (!TileIdPattern.IsMatch(tile.Id))
            return (false, "ID must be snake_case starting with a letter");

        if (string.IsNullOrWhiteSpace(tile.Name))
            return (false, "Name is required");

        var validPassability = new[] { "passable", "solid", "partially_passable" };
        if (!validPassability.Contains(tile.Passability.ToLowerInvariant()))
            return (false, "Invalid passability value");

        var validLayers = new[] { "terrain", "decoration", "structure", "effects" };
        if (!validLayers.Contains(tile.Layer.ToLowerInvariant()))
            return (false, "Invalid layer value");

        // Validate biomes against loaded biomes (if any loaded) or allow all
        if (_biomes.Count > 0)
        {
            foreach (var biome in tile.Biomes)
            {
                if (!_biomes.ContainsKey(biome.ToLowerInvariant()))
                    return (false, $"Invalid biome: {biome}");
            }
        }

        return (true, "Valid");
    }

    public (bool success, string message) SaveTiles()
    {
        return SaveTiles(compileAtlas: true);
    }

    public (bool success, string message) SaveTiles(bool compileAtlas)
    {
        // Validate all tiles first
        foreach (var tile in _tiles.Values)
        {
            var (valid, msg) = ValidateTile(tile);
            if (!valid)
                return (false, $"Tile '{tile.Id}': {msg}");
        }

        try
        {
            var data = new TileRegistryData
            {
                Schema = "./tiles.schema.json",
                Version = _version,
                Tileset = TilesetPath,
                Biomes = _biomes.Count > 0 ? _biomes.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new BiomeDataJson
                    {
                        DisplayName = kvp.Value.DisplayName,
                        Signature = kvp.Value.Signature,
                        BlockedPercentage = kvp.Value.BlockedPercentage,
                        PassableTiles = NormalizeWeights(kvp.Value.PassableTiles),
                        BlockedTiles = NormalizeWeights(kvp.Value.BlockedTiles)
                    }) : null,
                AutoTileFormats = _customAutoTileFormats.Count > 0 ? _customAutoTileFormats.Select(f =>
                    {
                        // Only serialize non-1x1 variant sizes
                        var nonDefaultSizes = f.VariantMappings
                            .Where(vm => vm.Value.SizeX != 1 || vm.Value.SizeY != 1)
                            .ToDictionary(
                                vm => vm.Key.ToString(),
                                vm => new Vector2IData { X = vm.Value.SizeX, Y = vm.Value.SizeY }
                            );

                        return new AutoTileFormatDataJson
                        {
                            Name = f.Name,
                            BitmaskType = f.BitmaskType switch
                            {
                                Features.Worldgen.AutoTiling.BitmaskType.Edge4 => "edge4",
                                Features.Worldgen.AutoTiling.BitmaskType.Full8 => "full8",
                                _ => "corner4"
                            },
                            AllowedBitmasks = f.AllowedBitmasks.OrderBy(b => b).ToArray(),
                            VariantSizes = nonDefaultSizes.Count > 0 ? nonDefaultSizes : null
                        };
                    }).ToList() : null,
                Tiles = _tiles.Values.Select(t => new TileData
                {
                    Id = t.Id,
                    Name = t.Name,
                    Passability = t.Passability.ToLowerInvariant(),
                    AtlasCoords = new Vector2IData { X = t.AtlasX, Y = t.AtlasY },
                    SourceId = t.SourceId,
                    Layer = t.Layer.ToLowerInvariant(),
                    Elevation = t.Elevation,
                    IsTransparent = t.IsTransparent,
                    Biomes = t.Biomes.Count > 0 ? t.Biomes : null,
                    Size = (t.SizeX != 1 || t.SizeY != 1) ? new Vector2IData { X = t.SizeX, Y = t.SizeY } : null,
                    SourceScale = t.SourceScale != 1.0f ? t.SourceScale : null,
                    AutoTileVariants = t.HasAutoTileVariants
                        ? t.AutoTileVariants!
                            .Select(v => v.HasValue
                                ? new Vector2IData { X = v.Value.X, Y = v.Value.Y }
                                : null)
                            .ToArray()
                        : null,
                    DecorationDensity = t.DecorationDensity < 1.0f ? t.DecorationDensity : null,
                    AutoTileFormat = t.AutoTileFormat != "corner16" ? t.AutoTileFormat : null,
                    Variations = t.HasVariations
                        ? t.Variations!.Select(v => new Vector2IData { X = v.X, Y = v.Y }).ToArray()
                        : null,
                    VariationMode = t.VariationMode != "perinstance" ? t.VariationMode : null,
                    Animation = t.HasAnimation
                        ? new AnimationData
                        {
                            Frames = t.AnimationFrames!.Select(f => new Vector2IData { X = f.X, Y = f.Y }).ToArray(),
                            FrameDuration = t.AnimationFrameDuration
                        }
                        : null,
                    TileMode = t.TileMode != "plain" ? t.TileMode : null,
                    Dominance = t.Dominance,
                    InnerTerrain = t.InnerTerrainId,
                    OuterTerrain = t.OuterTerrainId,
                    Description = t.Description
                }).ToList()
            };

            var json = JsonSerializer.Serialize(data, CreateWriteOptions());
            var absolutePath = ProjectSettings.GlobalizePath(TilesPath);
            File.WriteAllText(absolutePath, json);

            GD.Print($"[TileEditorService] Saved {_tiles.Count} tiles to {TilesPath}");

            // Compile atlas after saving tiles.json
            if (compileAtlas)
            {
                var atlasResult = CompileAtlas();
                if (!atlasResult.success)
                {
                    GD.PrintErr($"[TileEditorService] Atlas compilation failed: {atlasResult.message}");
                    return (true, $"Saved tiles, but atlas compilation failed: {atlasResult.message}");
                }
            }

            return (true, "Saved successfully");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[TileEditorService] Error saving tiles: {ex.Message}");
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Compiles tiles into a single atlas texture with coordinate mapping.
    /// Called automatically after SaveTiles() unless compileAtlas=false.
    /// </summary>
    public (bool success, string message) CompileAtlas()
    {
        if (_tileSet == null)
            return (false, "No TileSet loaded - cannot compile atlas");

        var compiler = new TileAtlasCompiler();
        return compiler.CompileAtlas(this);
    }

    // JSON data model classes
    private sealed class TileRegistryData
    {
        [JsonPropertyName("$schema")] public string? Schema { get; set; }

        [JsonPropertyName("version")] public string? Version { get; set; }

        [JsonPropertyName("tileset")] public string? Tileset { get; set; }

        [JsonPropertyName("biomes")] public Dictionary<string, BiomeDataJson>? Biomes { get; set; }

        [JsonPropertyName("autoTileFormats")] public List<AutoTileFormatDataJson>? AutoTileFormats { get; set; }

        [JsonPropertyName("tiles")] public List<TileData>? Tiles { get; set; }
    }

    private sealed class AutoTileFormatDataJson
    {
        [JsonPropertyName("name")] public string? Name { get; set; }

        [JsonPropertyName("bitmaskType")] public string? BitmaskType { get; set; }

        [JsonPropertyName("allowedBitmasks")] public int[]? AllowedBitmasks { get; set; }

        /// <summary>
        /// Variant size mappings keyed by bitmask. Only non-1x1 sizes are serialized.
        /// </summary>
        [JsonPropertyName("variantSizes")] public Dictionary<string, Vector2IData>? VariantSizes { get; set; }
    }

    private sealed class BiomeDataJson
    {
        [JsonPropertyName("displayName")] public string? DisplayName { get; set; }

        [JsonPropertyName("signature")] public float[]? Signature { get; set; }

        [JsonPropertyName("blockedPercentage")] public float BlockedPercentage { get; set; }

        [JsonPropertyName("passableTiles")] public Dictionary<string, float>? PassableTiles { get; set; }

        [JsonPropertyName("blockedTiles")] public Dictionary<string, float>? BlockedTiles { get; set; }
    }

    private sealed class TileData
    {
        [JsonPropertyName("id")] public string? Id { get; set; }

        [JsonPropertyName("name")] public string? Name { get; set; }

        [JsonPropertyName("passability")] public string? Passability { get; set; }

        [JsonPropertyName("atlasCoords")] public Vector2IData? AtlasCoords { get; set; }

        [JsonPropertyName("sourceId")] public int? SourceId { get; set; }

        [JsonPropertyName("layer")] public string? Layer { get; set; }

        [JsonPropertyName("elevation")] public float? Elevation { get; set; }

        [JsonPropertyName("isTransparent")] public bool? IsTransparent { get; set; }

        [JsonPropertyName("biomes")] public List<string>? Biomes { get; set; }

        [JsonPropertyName("size")] public Vector2IData? Size { get; set; }

        [JsonPropertyName("autoTileVariants")] public Vector2IData?[]? AutoTileVariants { get; set; }

        [JsonPropertyName("decorationDensity")] public float? DecorationDensity { get; set; }

        [JsonPropertyName("autoTileFormat")] public string? AutoTileFormat { get; set; }

        [JsonPropertyName("variations")] public Vector2IData[]? Variations { get; set; }

        [JsonPropertyName("variationMode")] public string? VariationMode { get; set; }

        [JsonPropertyName("animation")] public AnimationData? Animation { get; set; }

        [JsonPropertyName("tileMode")] public string? TileMode { get; set; }

        [JsonPropertyName("dominance")] public int? Dominance { get; set; }

        [JsonPropertyName("sourceScale")] public float? SourceScale { get; set; }

        /// <summary>
        /// For auto-tiles: the terrain whose border is shown (higher dominance terrain).
        /// Null means use dominance-based resolution at runtime.
        /// </summary>
        [JsonPropertyName("innerTerrain")] public string? InnerTerrain { get; set; }

        /// <summary>Background terrain for auto-tiles; "*" is compositable, null uses dominance resolution.</summary>
        [JsonPropertyName("outerTerrain")] public string? OuterTerrain { get; set; }

        /// <summary>
        /// Visual appearance description for artists and AI image generators.
        /// </summary>
        [JsonPropertyName("description")] public string? Description { get; set; }
    }

    private sealed class AnimationData
    {
        [JsonPropertyName("frames")] public Vector2IData[]? Frames { get; set; }

        [JsonPropertyName("frameDuration")] public float FrameDuration { get; set; } = 0.2f;
    }

    private sealed class Vector2IData
    {
        [JsonPropertyName("x")] public int X { get; set; }

        [JsonPropertyName("y")] public int Y { get; set; }
    }
}

/// <summary>
/// Mutable tile data for editing
/// </summary>
public class EditableTile
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Passability { get; set; } = "passable";
    public int AtlasX { get; set; }
    public int AtlasY { get; set; }
    public int SourceId { get; set; } = 4;
    public string Layer { get; set; } = "terrain";
    public float Elevation { get; set; }
    public bool IsTransparent { get; set; } = true;
    public List<string> Biomes { get; set; } = new();
    public int SizeX { get; set; } = 1;
    public int SizeY { get; set; } = 1;

    /// <summary>Source texture scale: 0.5 for 32px, 1.0 for 16px, 2.0 for 8px tiles.</summary>
    public float SourceScale { get; set; } = 1.0f;

    /// <summary>Auto-tile atlas coordinates by bitmask; null elements use base coordinates.</summary>
    public Vector2I?[]? AutoTileVariants { get; set; }

    /// <summary>Auto-tile format name; built-ins and registered custom formats are supported.</summary>
    public string AutoTileFormat { get; set; } = "corner16";

    /// <summary>Per-bitmask variant size/offset definitions; null or empty uses 1x1 variants.</summary>
    public Dictionary<int, EditableVariantDefinition>? CustomVariantDefinitions { get; set; }

    /// <summary>
    /// Returns true if this tile has any auto-tile variants defined.
    /// </summary>
    public bool HasAutoTileVariants => AutoTileVariants?.Any(v => v.HasValue) == true;

    /// <summary>
    /// Get the expected number of variants based on the format.
    /// </summary>
    public int ExpectedVariantCount => AutoTileFormat == "blob47" ? 47 : 16;

    /// <summary>
    /// Probability (0.0-1.0) of this decoration tile appearing on valid positions.
    /// Only meaningful for decoration layer tiles. Default 1.0 = 100% coverage.
    /// </summary>
    public float DecorationDensity { get; set; } = 1.0f;

    /// <summary>
    /// Visual variations for this tile (different atlas coordinates for the same tile type).
    /// Null means no variations (always use base AtlasCoords).
    /// </summary>
    public Vector2I[]? Variations { get; set; }

    /// <summary>Variation mode: perinstance randomizes placements; pergeneration shares one choice.</summary>
    public string VariationMode { get; set; } = "perinstance";

    /// <summary>
    /// Whether this tile has visual variations.
    /// </summary>
    public bool HasVariations => Variations != null && Variations.Length > 0;

    /// <summary>
    /// Animation frame atlas coordinates. Null means no animation.
    /// </summary>
    public Vector2I[]? AnimationFrames { get; set; }

    /// <summary>
    /// Duration of each animation frame in seconds.
    /// </summary>
    public float AnimationFrameDuration { get; set; } = 0.2f;

    /// <summary>
    /// Whether this tile has animation frames.
    /// </summary>
    public bool HasAnimation => AnimationFrames != null && AnimationFrames.Length > 1;

    /// <summary>
    /// The tile mode determining which features are active.
    /// Stored as lowercase string for JSON serialization.
    /// </summary>
    public string TileMode { get; set; } = "plain";

    /// <summary>Terrain transition dominance; higher values render above lower values.</summary>
    public int? Dominance { get; set; }

    /// <summary>
    /// For auto-tiles: the terrain whose border is shown (higher dominance terrain).
    /// Null means use dominance-based resolution at runtime.
    /// </summary>
    public string? InnerTerrainId { get; set; }

    /// <summary>Background terrain for auto-tiles; "*" is compositable, null uses dominance resolution.</summary>
    public string? OuterTerrainId { get; set; }

    /// <summary>
    /// Visual appearance description for artists and AI image generators.
    /// Should include color, texture, and notable visual features.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Returns true if this auto-tile is compositable (OuterTerrainId is "*"),
    /// meaning its border should be composited onto any base terrain at atlas compile time.
    /// </summary>
    public bool IsCompositable => OuterTerrainId == "*";

    /// <summary>
    /// Returns true if this auto-tile has a fixed transition (OuterTerrainId is a specific tile ID),
    /// meaning it has a baked background and should be used as-is.
    /// </summary>
    public bool IsFixedTransition => !string.IsNullOrEmpty(OuterTerrainId) && OuterTerrainId != "*";

    /// <summary>
    /// Returns true if this tile has custom variant definitions with non-standard sizes or offsets.
    /// </summary>
    public bool HasCustomVariantDefinitions => CustomVariantDefinitions?.Count > 0;

    /// <summary>
    /// Gets the variant definition for a specific bitmask index.
    /// Returns a default definition with the atlas coords if no custom definition exists.
    /// </summary>
    public EditableVariantDefinition GetVariantDefinition(int bitmaskIndex)
    {
        // Check for custom definition first
        if (CustomVariantDefinitions != null &&
            CustomVariantDefinitions.TryGetValue(bitmaskIndex, out var customDef))
        {
            return customDef;
        }

        // Fall back to simple atlas coords from AutoTileVariants
        if (AutoTileVariants != null && bitmaskIndex < AutoTileVariants.Length &&
            AutoTileVariants[bitmaskIndex].HasValue)
        {
            var coords = AutoTileVariants[bitmaskIndex]!.Value;
            return new EditableVariantDefinition
            {
                AtlasX = coords.X,
                AtlasY = coords.Y,
                SizeX = 1,
                SizeY = 1,
                OffsetX = 0,
                OffsetY = 0
            };
        }

        // Default to base tile coords
        return new EditableVariantDefinition
        {
            AtlasX = AtlasX,
            AtlasY = AtlasY,
            SizeX = 1,
            SizeY = 1,
            OffsetX = 0,
            OffsetY = 0
        };
    }

    /// <summary>
    /// Sets a custom variant definition for a specific bitmask index.
    /// </summary>
    public void SetVariantDefinition(int bitmaskIndex, EditableVariantDefinition definition)
    {
        CustomVariantDefinitions ??= new Dictionary<int, EditableVariantDefinition>();
        CustomVariantDefinitions[bitmaskIndex] = definition;

        // Also update AutoTileVariants for backward compatibility
        AutoTileVariants ??= new Vector2I?[ExpectedVariantCount];
        if (bitmaskIndex < AutoTileVariants.Length)
        {
            AutoTileVariants[bitmaskIndex] = new Vector2I(definition.AtlasX, definition.AtlasY);
        }
    }

    public EditableTile Clone()
    {
        var clone = new EditableTile
        {
            Id = Id,
            Name = Name,
            Passability = Passability,
            AtlasX = AtlasX,
            AtlasY = AtlasY,
            SourceId = SourceId,
            Layer = Layer,
            Elevation = Elevation,
            IsTransparent = IsTransparent,
            Biomes = new List<string>(Biomes),
            SizeX = SizeX,
            SizeY = SizeY,
            SourceScale = SourceScale,
            DecorationDensity = DecorationDensity,
            AutoTileFormat = AutoTileFormat,
            VariationMode = VariationMode,
            AnimationFrameDuration = AnimationFrameDuration,
            TileMode = TileMode,
            Dominance = Dominance,
            InnerTerrainId = InnerTerrainId,
            OuterTerrainId = OuterTerrainId,
            Description = Description
        };

        if (AutoTileVariants != null)
        {
            clone.AutoTileVariants = new Vector2I?[AutoTileVariants.Length];
            Array.Copy(AutoTileVariants, clone.AutoTileVariants, AutoTileVariants.Length);
        }

        if (Variations != null)
        {
            clone.Variations = new Vector2I[Variations.Length];
            Array.Copy(Variations, clone.Variations, Variations.Length);
        }

        if (AnimationFrames != null)
        {
            clone.AnimationFrames = new Vector2I[AnimationFrames.Length];
            Array.Copy(AnimationFrames, clone.AnimationFrames, AnimationFrames.Length);
        }

        if (CustomVariantDefinitions != null)
        {
            clone.CustomVariantDefinitions = new Dictionary<int, EditableVariantDefinition>();
            foreach (var (key, value) in CustomVariantDefinitions)
            {
                clone.CustomVariantDefinitions[key] = value.Clone();
            }
        }

        return clone;
    }
}

/// <summary>
/// Information about a TileSetAtlasSource for UI display
/// </summary>
public class AtlasSourceInfo
{
    public int SourceId { get; set; }
    public string DisplayName { get; set; } = "";
    public TileSetAtlasSource? Source { get; set; }
}

/// <summary>
/// Mutable biome data for editing in the tile editor
/// </summary>
public class EditableBiome
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public float[] Signature { get; set; } = new float[8];
    public float BlockedPercentage { get; set; } = 0.2f;
    public Dictionary<string, float> PassableTiles { get; set; } = new();
    public Dictionary<string, float> BlockedTiles { get; set; } = new();

    public EditableBiome Clone()
    {
        return new EditableBiome
        {
            Id = Id,
            DisplayName = DisplayName,
            Signature = (float[])Signature.Clone(),
            BlockedPercentage = BlockedPercentage,
            PassableTiles = new Dictionary<string, float>(PassableTiles),
            BlockedTiles = new Dictionary<string, float>(BlockedTiles)
        };
    }
}

/// <summary>
/// Mutable variant definition for editing auto-tile variants.
/// Corresponds to the immutable VariantDefinition record in the runtime.
/// </summary>
public class EditableVariantDefinition
{
    /// <summary>Atlas X coordinate for this variant.</summary>
    public int AtlasX { get; set; }

    /// <summary>Atlas Y coordinate for this variant.</summary>
    public int AtlasY { get; set; }

    /// <summary>Width in cells (default 1).</summary>
    public int SizeX { get; set; } = 1;

    /// <summary>Height in cells (default 1).</summary>
    public int SizeY { get; set; } = 1;

    /// <summary>X anchor offset from logical cell position.</summary>
    public int OffsetX { get; set; }

    /// <summary>Y anchor offset from logical cell position (negative = above).</summary>
    public int OffsetY { get; set; }

    /// <summary>Override atlas region width (null = use tileset default).</summary>
    public int? AtlasRegionWidth { get; set; }

    /// <summary>Override atlas region height (null = use tileset default).</summary>
    public int? AtlasRegionHeight { get; set; }

    /// <summary>Returns true if this is a multi-cell variant.</summary>
    public bool IsMultiCell => SizeX > 1 || SizeY > 1;

    /// <summary>Returns true if this variant has a non-zero offset.</summary>
    public bool HasOffset => OffsetX != 0 || OffsetY != 0;

    /// <summary>Gets the atlas coordinates as a Vector2I.</summary>
    public Vector2I AtlasCoords => new(AtlasX, AtlasY);

    /// <summary>Gets the size as a Vector2I.</summary>
    public Vector2I Size => new(SizeX, SizeY);

    /// <summary>Gets the offset as a Vector2I.</summary>
    public Vector2I Offset => new(OffsetX, OffsetY);

    /// <summary>
    /// Converts to the immutable runtime VariantDefinition.
    /// </summary>
    public Features.Worldgen.AutoTiling.VariantDefinition ToVariantDefinition()
    {
        Vector2I? atlasRegionSize = (AtlasRegionWidth.HasValue || AtlasRegionHeight.HasValue)
            ? new Vector2I(AtlasRegionWidth ?? 0, AtlasRegionHeight ?? 0)
            : null;

        return new Features.Worldgen.AutoTiling.VariantDefinition(
            AtlasCoords,
            Size,
            Offset,
            atlasRegionSize
        );
    }

    /// <summary>
    /// Creates from an immutable runtime VariantDefinition.
    /// </summary>
    public static EditableVariantDefinition FromVariantDefinition(Features.Worldgen.AutoTiling.VariantDefinition def)
    {
        return new EditableVariantDefinition
        {
            AtlasX = def.AtlasCoords.X,
            AtlasY = def.AtlasCoords.Y,
            SizeX = def.Size.X,
            SizeY = def.Size.Y,
            OffsetX = def.Offset.X,
            OffsetY = def.Offset.Y,
            AtlasRegionWidth = def.AtlasRegionSize?.X,
            AtlasRegionHeight = def.AtlasRegionSize?.Y
        };
    }

    /// <summary>
    /// Creates a deep copy of this variant definition.
    /// </summary>
    public EditableVariantDefinition Clone()
    {
        return new EditableVariantDefinition
        {
            AtlasX = AtlasX,
            AtlasY = AtlasY,
            SizeX = SizeX,
            SizeY = SizeY,
            OffsetX = OffsetX,
            OffsetY = OffsetY,
            AtlasRegionWidth = AtlasRegionWidth,
            AtlasRegionHeight = AtlasRegionHeight
        };
    }
}
#endif
