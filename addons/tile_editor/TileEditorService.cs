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
    public delegate void AutoTileConfigModifiedEventHandler(string baseTileId);

    [Signal]
    public delegate void BlobConfigModifiedEventHandler();

    [Signal]
    public delegate void TileAddedEventHandler(string tileId);

    [Signal]
    public delegate void TileModifiedEventHandler(string tileId);

    [Signal]
    public delegate void TileRemovedEventHandler(string tileId);

    // Signals
    [Signal]
    public delegate void TilesLoadedEventHandler();

    private const string TilesPath = "res://Data/Tiles/tiles.json";
    private const string DefaultTilesetPath = "res://Assets/Terrain/TileSets/ByPack/FantasyDreamland.tres";
    private static readonly Regex TileIdPattern = new(@"^[a-z][a-z0-9_]*$", RegexOptions.Compiled);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly Dictionary<string, EditableAutoTileConfig> _autoTileConfigs = new();

    private readonly Dictionary<string, EditableTile> _tiles = new();
    private EditableBlobConfig _blobConfig = new();
    private TileSet? _tileSet;
    private string _version = "1.0";

    public int TileCount => _tiles.Count;
    public IEnumerable<EditableTile> AllTiles => _tiles.Values;
    public string TilesetPath { get; private set; } = DefaultTilesetPath;
    public TileSet? TileSet => _tileSet;

    // Blob config accessor
    public EditableBlobConfig BlobConfig => _blobConfig;

    // Auto-tile config accessors
    public IEnumerable<EditableAutoTileConfig> AllAutoTileConfigs => _autoTileConfigs.Values;

    public void LoadTiles()
    {
        _tiles.Clear();
        _autoTileConfigs.Clear();
        _blobConfig = new EditableBlobConfig();

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
            var data = JsonSerializer.Deserialize<TileRegistryData>(json, JsonOptions);

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
                    SizeY = tileData.Size?.Y ?? 1
                };

                // Load auto-tile variants if present
                if (tileData.AutoTileVariants != null && tileData.AutoTileVariants.Any(v => v != null))
                {
                    tile.AutoTileVariants = new Vector2I?[16];
                    for (var i = 0; i < Math.Min(16, tileData.AutoTileVariants.Length); i++)
                    {
                        var v = tileData.AutoTileVariants[i];
                        if (v != null)
                            tile.AutoTileVariants[i] = new Vector2I(v.X, v.Y);
                    }
                }

                // Load decoration density (default 1.0 if not specified)
                if (tileData.DecorationDensity.HasValue)
                    tile.DecorationDensity = Math.Clamp(tileData.DecorationDensity.Value, 0f, 1f);

                // Load per-tile blob settings if present
                if (tileData.BlobSettings != null)
                {
                    tile.BlobSettings = new EditableBlobConfig
                    {
                        Enabled = tileData.BlobSettings.Enabled,
                        NoiseScale = tileData.BlobSettings.NoiseScale,
                        ClusterStrength = tileData.BlobSettings.ClusterStrength,
                        MinBlobSize = tileData.BlobSettings.MinBlobSize,
                        MaxBlobSize = tileData.BlobSettings.MaxBlobSize
                    };
                }

                _tiles[tile.Id] = tile;
            }

            // Load blob generation config
            if (data.BlobGeneration != null)
            {
                _blobConfig = new EditableBlobConfig
                {
                    Enabled = data.BlobGeneration.Enabled,
                    NoiseScale = data.BlobGeneration.NoiseScale,
                    ClusterStrength = data.BlobGeneration.ClusterStrength,
                    MinBlobSize = data.BlobGeneration.MinBlobSize,
                    MaxBlobSize = data.BlobGeneration.MaxBlobSize
                };
            }

            // Load auto-tile configs
            if (data.AutoTileConfigs != null)
                foreach (var configData in data.AutoTileConfigs)
                {
                    if (string.IsNullOrEmpty(configData.BaseTileId)) continue;

                    var config = new EditableAutoTileConfig
                    {
                        BaseTileId = configData.BaseTileId, DisplayName = configData.DisplayName ?? ""
                    };

                    if (configData.Variants != null)
                        for (var i = 0; i < Math.Min(16, configData.Variants.Length); i++)
                            config.Variants[i] = configData.Variants[i];

                    _autoTileConfigs[config.BaseTileId] = config;
                }

            GD.Print($"[TileEditorService] Loaded {_tiles.Count} tiles, {_autoTileConfigs.Count} auto-tile configs");
            EmitSignal(SignalName.TilesLoaded);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[TileEditorService] Error loading tiles: {ex.Message}");
            EmitSignal(SignalName.TilesLoaded);
        }
    }

    /// <summary>
    /// Get the texture for a tile from the TileSet
    /// </summary>
    public Texture2D? GetTileTexture(EditableTile tile)
    {
        if (_tileSet == null) return null;

        var source = _tileSet.GetSource(tile.SourceId) as TileSetAtlasSource;
        return source?.Texture;
    }

    /// <summary>
    /// Get the texture region for a tile (supports multi-tile sizes)
    /// </summary>
    public Rect2I GetTileTextureRegion(EditableTile tile)
    {
        if (_tileSet == null) return new Rect2I(0, 0, 16, 16);

        var source = _tileSet.GetSource(tile.SourceId) as TileSetAtlasSource;
        if (source == null) return new Rect2I(0, 0, 16, 16);

        var tileSize = _tileSet.TileSize;
        var atlasCoords = new Vector2I(tile.AtlasX, tile.AtlasY);

        // Use the tile's size for multi-tile support
        var regionSize = new Vector2I(tileSize.X * tile.SizeX, tileSize.Y * tile.SizeY);
        return new Rect2I(atlasCoords * tileSize, regionSize);
    }

    /// <summary>
    /// Get all available TileSetAtlasSource entries with descriptive info
    /// </summary>
    public List<AtlasSourceInfo> GetAvailableAtlasSources()
    {
        var sources = new List<AtlasSourceInfo>();
        if (_tileSet == null) return sources;

        var sourceCount = _tileSet.GetSourceCount();
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

        return sources;
    }

    /// <summary>
    /// Get a specific TileSetAtlasSource by ID
    /// </summary>
    public TileSetAtlasSource? GetAtlasSource(int sourceId)
    {
        return _tileSet?.GetSource(sourceId) as TileSetAtlasSource;
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
        if (_tileSet == null) return false;

        // Verify no tiles are using this source
        if (_tiles.Values.Any(t => t.SourceId == sourceId))
        {
            GD.PrintErr($"[TileEditorService] Cannot remove source {sourceId}: tiles are using it");
            return false;
        }

        _tileSet.RemoveSource(sourceId);
        var saveResult = ResourceSaver.Save(_tileSet, TilesetPath);

        if (saveResult == Error.Ok)
        {
            GD.Print($"[TileEditorService] Removed source {sourceId} and saved TileSet");
            return true;
        }

        GD.PrintErr($"[TileEditorService] Failed to save TileSet after removing source: {saveResult}");
        return false;
    }

    public EditableTile? GetTile(string id) => _tiles.GetValueOrDefault(id);

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

    // ===== Blob Config Methods =====

    public void UpdateBlobConfig(EditableBlobConfig config)
    {
        _blobConfig = config;
        EmitSignal(SignalName.BlobConfigModified);
    }

    // ===== Auto-Tile Config Methods =====

    public EditableAutoTileConfig? GetAutoTileConfig(string baseTileId) =>
        _autoTileConfigs.GetValueOrDefault(baseTileId);

    public void AddAutoTileConfig(EditableAutoTileConfig config)
    {
        _autoTileConfigs[config.BaseTileId] = config;
        EmitSignal(SignalName.AutoTileConfigModified, config.BaseTileId);
    }

    public void UpdateAutoTileConfig(EditableAutoTileConfig config)
    {
        _autoTileConfigs[config.BaseTileId] = config;
        EmitSignal(SignalName.AutoTileConfigModified, config.BaseTileId);
    }

    public void RemoveAutoTileConfig(string baseTileId)
    {
        if (_autoTileConfigs.Remove(baseTileId)) EmitSignal(SignalName.AutoTileConfigModified, baseTileId);
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

        var validBiomes = new[] { "plains", "forest", "desert", "tundra", "swamp", "mountains" };
        foreach (var biome in tile.Biomes)
        {
            if (!validBiomes.Contains(biome.ToLowerInvariant()))
                return (false, $"Invalid biome: {biome}");
        }

        return (true, "Valid");
    }

    public (bool success, string message) SaveTiles()
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
                    AutoTileVariants = t.HasAutoTileVariants
                        ? t.AutoTileVariants!.Select(v => v.HasValue ? new Vector2IData { X = v.Value.X, Y = v.Value.Y } : null).ToArray()
                        : null,
                    DecorationDensity = t.DecorationDensity < 1.0f ? t.DecorationDensity : null,
                    BlobSettings = t.BlobSettings != null ? new BlobGenerationData
                    {
                        Enabled = t.BlobSettings.Enabled,
                        NoiseScale = t.BlobSettings.NoiseScale,
                        ClusterStrength = t.BlobSettings.ClusterStrength,
                        MinBlobSize = t.BlobSettings.MinBlobSize,
                        MaxBlobSize = t.BlobSettings.MaxBlobSize
                    } : null
                }).ToList(),

                // Blob generation config
                BlobGeneration = new BlobGenerationData
                {
                    Enabled = _blobConfig.Enabled,
                    NoiseScale = _blobConfig.NoiseScale,
                    ClusterStrength = _blobConfig.ClusterStrength,
                    MinBlobSize = _blobConfig.MinBlobSize,
                    MaxBlobSize = _blobConfig.MaxBlobSize
                },

                // Auto-tile configs
                AutoTileConfigs = _autoTileConfigs.Count > 0
                    ? _autoTileConfigs.Values.Select(c => new AutoTileConfigData
                    {
                        BaseTileId = c.BaseTileId,
                        DisplayName = string.IsNullOrEmpty(c.DisplayName) ? null : c.DisplayName,
                        Variants = c.Variants.Any(v => !string.IsNullOrEmpty(v)) ? c.Variants : null
                    }).ToList()
                    : null
            };

            var json = JsonSerializer.Serialize(data, WriteOptions);
            var absolutePath = ProjectSettings.GlobalizePath(TilesPath);
            File.WriteAllText(absolutePath, json);

            GD.Print($"[TileEditorService] Saved {_tiles.Count} tiles to {TilesPath}");
            return (true, "Saved successfully");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[TileEditorService] Error saving tiles: {ex.Message}");
            return (false, ex.Message);
        }
    }

    // JSON data model classes
    private sealed class TileRegistryData
    {
        [JsonPropertyName("$schema")] public string? Schema { get; set; }

        [JsonPropertyName("version")] public string? Version { get; set; }

        [JsonPropertyName("tileset")] public string? Tileset { get; set; }

        [JsonPropertyName("tiles")] public List<TileData>? Tiles { get; set; }

        [JsonPropertyName("blobGeneration")] public BlobGenerationData? BlobGeneration { get; set; }

        [JsonPropertyName("autoTileConfigs")] public List<AutoTileConfigData>? AutoTileConfigs { get; set; }
    }

    private sealed class AutoTileConfigData
    {
        [JsonPropertyName("baseTileId")] public string? BaseTileId { get; set; }

        [JsonPropertyName("displayName")] public string? DisplayName { get; set; }

        [JsonPropertyName("variants")] public string?[]? Variants { get; set; }
    }

    private sealed class BlobGenerationData
    {
        [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;

        [JsonPropertyName("noiseScale")] public float NoiseScale { get; set; } = 0.15f;

        [JsonPropertyName("clusterStrength")] public float ClusterStrength { get; set; } = 0.7f;

        [JsonPropertyName("minBlobSize")] public int MinBlobSize { get; set; } = 3;

        [JsonPropertyName("maxBlobSize")] public int MaxBlobSize { get; set; } = 12;
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

        [JsonPropertyName("blobSettings")] public BlobGenerationData? BlobSettings { get; set; }
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

    /// <summary>
    /// Auto-tile variant atlas coordinates indexed by bitmask.
    /// For Corner16: 16 entries indexed by 4-bit corner mask.
    /// For Blob47: 47 entries indexed by blob index.
    /// Null array means no auto-tiling. Null elements use the base tile's atlas coords.
    /// </summary>
    public Vector2I?[]? AutoTileVariants { get; set; }

    /// <summary>
    /// The auto-tile format: "corner16" (16 variants) or "blob47" (47 variants).
    /// Default is corner16 for backward compatibility.
    /// </summary>
    public string AutoTileFormat { get; set; } = "corner16";

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
    /// Per-tile blob generation settings. Null means use global defaults.
    /// </summary>
    public EditableBlobConfig? BlobSettings { get; set; }

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
            DecorationDensity = DecorationDensity,
            BlobSettings = BlobSettings?.Clone(),
            AutoTileFormat = AutoTileFormat
        };

        if (AutoTileVariants != null)
        {
            clone.AutoTileVariants = new Vector2I?[AutoTileVariants.Length];
            Array.Copy(AutoTileVariants, clone.AutoTileVariants, AutoTileVariants.Length);
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
/// Mutable blob generation configuration for editing
/// </summary>
public class EditableBlobConfig
{
    public bool Enabled { get; set; } = true;
    public float NoiseScale { get; set; } = 0.15f;
    public float ClusterStrength { get; set; } = 0.7f;
    public int MinBlobSize { get; set; } = 3;
    public int MaxBlobSize { get; set; } = 12;

    public EditableBlobConfig Clone() => new()
    {
        Enabled = Enabled,
        NoiseScale = NoiseScale,
        ClusterStrength = ClusterStrength,
        MinBlobSize = MinBlobSize,
        MaxBlobSize = MaxBlobSize
    };
}

/// <summary>
///     Mutable auto-tile configuration for editing.
///     Maps a base tile to 16 edge variants based on 4-bit NESW neighbor bitmask.
/// </summary>
public class EditableAutoTileConfig
{
    public string BaseTileId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string?[] Variants { get; set; } = new string?[16];

    public EditableAutoTileConfig Clone()
    {
        var clone = new EditableAutoTileConfig
        {
            BaseTileId = BaseTileId, DisplayName = DisplayName, Variants = new string?[16]
        };
        Array.Copy(Variants, clone.Variants, 16);
        return clone;
    }
}
#endif
