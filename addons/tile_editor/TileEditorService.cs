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
    private string _version = "1.0";
    private TileSet? _tileSet;

    public int TileCount => _tiles.Count;
    public IEnumerable<EditableTile> AllTiles => _tiles.Values;
    public string TilesetPath { get; private set; } = DefaultTilesetPath;
    public TileSet? TileSet => _tileSet;

    // Signals
    [Signal] public delegate void TilesLoadedEventHandler();
    [Signal] public delegate void TileModifiedEventHandler(string tileId);
    [Signal] public delegate void TileAddedEventHandler(string tileId);
    [Signal] public delegate void TileRemovedEventHandler(string tileId);

    public void LoadTiles()
    {
        _tiles.Clear();

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
                    Biomes = tileData.Biomes?.ToList() ?? new List<string>()
                };

                _tiles[tile.Id] = tile;
            }

            GD.Print($"[TileEditorService] Loaded {_tiles.Count} tiles");
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
    /// Get the texture region for a tile
    /// </summary>
    public Rect2I GetTileTextureRegion(EditableTile tile)
    {
        if (_tileSet == null) return new Rect2I(0, 0, 16, 16);

        var source = _tileSet.GetSource(tile.SourceId) as TileSetAtlasSource;
        if (source == null) return new Rect2I(0, 0, 16, 16);

        var tileSize = _tileSet.TileSize;
        var atlasCoords = new Vector2I(tile.AtlasX, tile.AtlasY);

        return new Rect2I(atlasCoords * tileSize, tileSize);
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
                    Biomes = t.Biomes.Count > 0 ? t.Biomes : null
                }).ToList()
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
        Biomes = new List<string>(Biomes)
    };
}
#endif
