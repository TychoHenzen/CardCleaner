#if TOOLS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CardCleaner.Scripts.Core.Enumeration;
using Godot;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// Service for loading, modifying, and saving tile data in the editor
/// </summary>
[Tool]
public partial class TileEditorService : RefCounted
{
    private const string TilesPath = "res://Data/Tiles/tiles.json";
    private const string DefaultTilesetPath = "res://Assets/Terrain/TileSets/ByPack/FantasyDreamland.tres";
    private static readonly Regex TileIdPattern = new(@"^[a-z][a-z0-9_]*$", RegexOptions.Compiled);

    private readonly Dictionary<string, EditableTile> _tiles = new();
    private readonly Dictionary<string, EditableTerrainGroup> _terrainGroups = new();
    private readonly List<EditableTransitionRule> _transitionRules = new();
    private EditableBlobConfig _blobConfig = new();
    private string _version = "1.0";
    private TileSet? _tileSet;

    public int TileCount => _tiles.Count;
    public IEnumerable<EditableTile> AllTiles => _tiles.Values;
    public string TilesetPath { get; private set; } = DefaultTilesetPath;
    public TileSet? TileSet => _tileSet;

    // Terrain group accessors
    public int TerrainGroupCount => _terrainGroups.Count;
    public IEnumerable<EditableTerrainGroup> AllTerrainGroups => _terrainGroups.Values;
    public EditableTerrainGroup? GetTerrainGroup(string id) => _terrainGroups.GetValueOrDefault(id);

    // Transition rule accessors
    public int TransitionRuleCount => _transitionRules.Count;
    public IReadOnlyList<EditableTransitionRule> AllTransitionRules => _transitionRules;

    // Blob config accessor
    public EditableBlobConfig BlobConfig => _blobConfig;

    // Signals
    [Signal] public delegate void TilesLoadedEventHandler();
    [Signal] public delegate void TileModifiedEventHandler(string tileId);
    [Signal] public delegate void TileAddedEventHandler(string tileId);
    [Signal] public delegate void TileRemovedEventHandler(string tileId);

    // Terrain signals
    [Signal] public delegate void TerrainGroupModifiedEventHandler(string groupId);
    [Signal] public delegate void TerrainGroupAddedEventHandler(string groupId);
    [Signal] public delegate void TerrainGroupRemovedEventHandler(string groupId);
    [Signal] public delegate void TransitionRuleModifiedEventHandler(int ruleIndex);
    [Signal] public delegate void TransitionRuleAddedEventHandler(int ruleIndex);
    [Signal] public delegate void TransitionRuleRemovedEventHandler(int ruleIndex);
    [Signal] public delegate void BlobConfigModifiedEventHandler();

    public void LoadTiles()
    {
        _tiles.Clear();
        _terrainGroups.Clear();
        _transitionRules.Clear();
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

                _tiles[tile.Id] = tile;
            }

            // Load terrain groups
            if (data.TerrainGroups != null)
            {
                foreach (var (groupId, groupData) in data.TerrainGroups)
                {
                    var group = new EditableTerrainGroup
                    {
                        Id = groupId,
                        Name = groupData.Name ?? groupId,
                        Priority = groupData.Priority,
                        Members = groupData.Members?.ToList() ?? new List<string>()
                    };
                    _terrainGroups[groupId] = group;
                }
            }

            // Load transition rules
            if (data.TransitionRules != null)
            {
                foreach (var ruleData in data.TransitionRules)
                {
                    if (string.IsNullOrEmpty(ruleData.FromGroup) || string.IsNullOrEmpty(ruleData.ToGroup))
                        continue;

                    var rule = new EditableTransitionRule
                    {
                        FromGroupId = ruleData.FromGroup,
                        ToGroupId = ruleData.ToGroup,
                        EdgeTiles = new Dictionary<int, string?>()
                    };

                    // Parse edge tiles (keys are string bitmask values "0"-"15")
                    if (ruleData.EdgeTiles != null)
                    {
                        foreach (var (key, tileId) in ruleData.EdgeTiles)
                        {
                            if (int.TryParse(key, out var bitmask) && bitmask >= 0 && bitmask <= 15)
                                rule.EdgeTiles[bitmask] = tileId;
                        }
                    }

                    _transitionRules.Add(rule);
                }
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

            GD.Print($"[TileEditorService] Loaded {_tiles.Count} tiles, {_terrainGroups.Count} terrain groups, {_transitionRules.Count} transition rules");
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
                SourceId = sourceId,
                DisplayName = $"Source {sourceId}: {textureName}",
                Source = source
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

    // ===== Terrain Group Methods =====

    public void UpdateTerrainGroup(EditableTerrainGroup group)
    {
        if (!_terrainGroups.ContainsKey(group.Id))
        {
            GD.PrintErr($"[TileEditorService] Terrain group not found: {group.Id}");
            return;
        }

        _terrainGroups[group.Id] = group;
        EmitSignal(SignalName.TerrainGroupModified, group.Id);
    }

    public bool AddTerrainGroup(EditableTerrainGroup group)
    {
        if (_terrainGroups.ContainsKey(group.Id))
        {
            GD.PrintErr($"[TileEditorService] Terrain group already exists: {group.Id}");
            return false;
        }

        _terrainGroups[group.Id] = group;
        EmitSignal(SignalName.TerrainGroupAdded, group.Id);
        return true;
    }

    public bool RemoveTerrainGroup(string id)
    {
        if (!_terrainGroups.Remove(id))
            return false;

        EmitSignal(SignalName.TerrainGroupRemoved, id);
        return true;
    }

    public bool AddTileToGroup(string groupId, string tileId)
    {
        if (!_terrainGroups.TryGetValue(groupId, out var group))
            return false;

        if (group.Members.Contains(tileId))
            return false;

        group.Members.Add(tileId);
        EmitSignal(SignalName.TerrainGroupModified, groupId);
        return true;
    }

    public bool RemoveTileFromGroup(string groupId, string tileId)
    {
        if (!_terrainGroups.TryGetValue(groupId, out var group))
            return false;

        if (!group.Members.Remove(tileId))
            return false;

        EmitSignal(SignalName.TerrainGroupModified, groupId);
        return true;
    }

    // ===== Transition Rule Methods =====

    public EditableTransitionRule? GetTransitionRule(int index)
    {
        if (index < 0 || index >= _transitionRules.Count)
            return null;
        return _transitionRules[index];
    }

    public void UpdateTransitionRule(int index, EditableTransitionRule rule)
    {
        if (index < 0 || index >= _transitionRules.Count)
        {
            GD.PrintErr($"[TileEditorService] Transition rule index out of range: {index}");
            return;
        }

        _transitionRules[index] = rule;
        EmitSignal(SignalName.TransitionRuleModified, index);
    }

    public int AddTransitionRule(EditableTransitionRule rule)
    {
        _transitionRules.Add(rule);
        var index = _transitionRules.Count - 1;
        EmitSignal(SignalName.TransitionRuleAdded, index);
        return index;
    }

    public bool RemoveTransitionRule(int index)
    {
        if (index < 0 || index >= _transitionRules.Count)
            return false;

        _transitionRules.RemoveAt(index);
        EmitSignal(SignalName.TransitionRuleRemoved, index);
        return true;
    }

    // ===== Blob Config Methods =====

    public void UpdateBlobConfig(EditableBlobConfig config)
    {
        _blobConfig = config;
        EmitSignal(SignalName.BlobConfigModified);
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

        // Validate terrain groups
        foreach (var group in _terrainGroups.Values)
        {
            var (valid, msg) = ValidateTerrainGroup(group);
            if (!valid)
                return (false, $"Terrain group '{group.Id}': {msg}");
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
                    Size = (t.SizeX != 1 || t.SizeY != 1) ? new Vector2IData { X = t.SizeX, Y = t.SizeY } : null
                }).ToList(),

                // Terrain groups
                TerrainGroups = _terrainGroups.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new TerrainGroupData
                    {
                        Name = kvp.Value.Name,
                        Priority = kvp.Value.Priority,
                        Members = kvp.Value.Members.Count > 0 ? kvp.Value.Members : null
                    }
                ),

                // Transition rules
                TransitionRules = _transitionRules.Select(r => new TransitionRuleData
                {
                    FromGroup = r.FromGroupId,
                    ToGroup = r.ToGroupId,
                    EdgeTiles = r.EdgeTiles.Count > 0
                        ? r.EdgeTiles.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value)
                        : null
                }).ToList(),

                // Blob generation config
                BlobGeneration = new BlobGenerationData
                {
                    Enabled = _blobConfig.Enabled,
                    NoiseScale = _blobConfig.NoiseScale,
                    ClusterStrength = _blobConfig.ClusterStrength,
                    MinBlobSize = _blobConfig.MinBlobSize,
                    MaxBlobSize = _blobConfig.MaxBlobSize
                }
            };

            var json = JsonSerializer.Serialize(data, WriteOptions);
            var absolutePath = ProjectSettings.GlobalizePath(TilesPath);
            File.WriteAllText(absolutePath, json);

            GD.Print($"[TileEditorService] Saved {_tiles.Count} tiles, {_terrainGroups.Count} terrain groups, {_transitionRules.Count} transition rules to {TilesPath}");
            return (true, "Saved successfully");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[TileEditorService] Error saving tiles: {ex.Message}");
            return (false, ex.Message);
        }
    }

    public (bool success, string message) ValidateTerrainGroup(EditableTerrainGroup group)
    {
        if (string.IsNullOrWhiteSpace(group.Id))
            return (false, "ID is required");

        if (!TileIdPattern.IsMatch(group.Id))
            return (false, "ID must be snake_case starting with a letter");

        if (string.IsNullOrWhiteSpace(group.Name))
            return (false, "Name is required");

        if (group.Priority < 0 || group.Priority > 1000)
            return (false, "Priority must be between 0 and 1000");

        return (true, "Valid");
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    // JSON data model classes
    private sealed class TileRegistryData
    {
        [JsonPropertyName("$schema")]
        public string? Schema { get; set; }

        [JsonPropertyName("version")]
        public string? Version { get; set; }

        [JsonPropertyName("tileset")]
        public string? Tileset { get; set; }

        [JsonPropertyName("tiles")]
        public List<TileData>? Tiles { get; set; }

        [JsonPropertyName("terrainGroups")]
        public Dictionary<string, TerrainGroupData>? TerrainGroups { get; set; }

        [JsonPropertyName("transitionRules")]
        public List<TransitionRuleData>? TransitionRules { get; set; }

        [JsonPropertyName("blobGeneration")]
        public BlobGenerationData? BlobGeneration { get; set; }
    }

    private sealed class TerrainGroupData
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("priority")]
        public int Priority { get; set; }

        [JsonPropertyName("members")]
        public List<string>? Members { get; set; }
    }

    private sealed class TransitionRuleData
    {
        [JsonPropertyName("fromGroup")]
        public string? FromGroup { get; set; }

        [JsonPropertyName("toGroup")]
        public string? ToGroup { get; set; }

        [JsonPropertyName("edgeTiles")]
        public Dictionary<string, string?>? EdgeTiles { get; set; }
    }

    private sealed class BlobGenerationData
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonPropertyName("noiseScale")]
        public float NoiseScale { get; set; } = 0.15f;

        [JsonPropertyName("clusterStrength")]
        public float ClusterStrength { get; set; } = 0.7f;

        [JsonPropertyName("minBlobSize")]
        public int MinBlobSize { get; set; } = 3;

        [JsonPropertyName("maxBlobSize")]
        public int MaxBlobSize { get; set; } = 12;
    }

    private sealed class TileData
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("passability")]
        public string? Passability { get; set; }

        [JsonPropertyName("atlasCoords")]
        public Vector2IData? AtlasCoords { get; set; }

        [JsonPropertyName("sourceId")]
        public int? SourceId { get; set; }

        [JsonPropertyName("layer")]
        public string? Layer { get; set; }

        [JsonPropertyName("elevation")]
        public float? Elevation { get; set; }

        [JsonPropertyName("isTransparent")]
        public bool? IsTransparent { get; set; }

        [JsonPropertyName("biomes")]
        public List<string>? Biomes { get; set; }

        [JsonPropertyName("size")]
        public Vector2IData? Size { get; set; }
    }

    private sealed class Vector2IData
    {
        [JsonPropertyName("x")]
        public int X { get; set; }

        [JsonPropertyName("y")]
        public int Y { get; set; }
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

    public EditableTile Clone() => new()
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
        SizeY = SizeY
    };
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
/// Mutable terrain group data for editing
/// </summary>
public class EditableTerrainGroup
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int Priority { get; set; } = 50;
    public List<string> Members { get; set; } = new();

    public EditableTerrainGroup Clone() => new()
    {
        Id = Id,
        Name = Name,
        Priority = Priority,
        Members = new List<string>(Members)
    };
}

/// <summary>
/// Mutable transition rule data for editing.
/// Maps 4-bit bitmask (0-15) to edge tile IDs.
/// Bitmask bits: N=1, E=2, S=4, W=8
/// </summary>
public class EditableTransitionRule
{
    public string FromGroupId { get; set; } = "";
    public string ToGroupId { get; set; } = "";

    /// <summary>
    /// Edge tiles keyed by 4-bit bitmask (0-15).
    /// Key is the bitmask value, value is the tile ID to render.
    /// </summary>
    public Dictionary<int, string?> EdgeTiles { get; set; } = new();

    /// <summary>
    /// Get the tile ID for a specific bitmask value
    /// </summary>
    public string? GetEdgeTile(int bitmask) => EdgeTiles.GetValueOrDefault(bitmask);

    /// <summary>
    /// Set the tile ID for a specific bitmask value
    /// </summary>
    public void SetEdgeTile(int bitmask, string? tileId)
    {
        if (string.IsNullOrEmpty(tileId))
            EdgeTiles.Remove(bitmask);
        else
            EdgeTiles[bitmask] = tileId;
    }

    public EditableTransitionRule Clone() => new()
    {
        FromGroupId = FromGroupId,
        ToGroupId = ToGroupId,
        EdgeTiles = new Dictionary<int, string?>(EdgeTiles)
    };
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
#endif
