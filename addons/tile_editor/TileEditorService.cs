#if TOOLS
using System;
using System.Collections.Generic;
using System.Linq;
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

    [Signal]
    public delegate void BiomesLoadedEventHandler();

    [Signal]
    public delegate void BiomeAddedEventHandler(string biomeId);

    [Signal]
    public delegate void BiomeModifiedEventHandler(string biomeId);

    [Signal]
    public delegate void BiomeRemovedEventHandler(string biomeId);

    [Signal]
    public delegate void AutoTileFormatsLoadedEventHandler();

    private const string TilesPath = "res://Data/Tiles/tiles.json";
    private const string DefaultTilesetPath =
        "res://Assets/Terrain/TileSets/ByPack/FantasyDreamland.tres";

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
            .OrderBy(format => format.IsBuiltIn ? 0 : 1)
            .ThenBy(format => format.Name)
            .Select(format => format.Name)
            .ToList();
    }

    /// <summary>
    /// Gets a format definition by name from the registry.
    /// Returns null if the format is not found.
    /// </summary>
    public Features.Worldgen.AutoTiling.AutoTileFormatDefinition? GetFormatDefinition(
        string formatName)
    {
        if (string.IsNullOrEmpty(formatName))
            return null;

        return Features.Worldgen.AutoTiling.AutoTileFormatRegistry.TryGet(
            formatName,
            out var format)
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
    /// Gets the expected number of variants for a format.
    /// Returns 16 for corner/edge formats, 47 for blob format.
    /// </summary>
    public int GetExpectedVariantCount(string formatName)
    {
        var format = GetFormatDefinition(formatName);
        return format?.AllowedBitmasks.Count ?? 16;
    }

    /// <summary>
    /// Gets the display name for a format (name with built-in indicator).
    /// </summary>
    public string GetFormatDisplayName(string formatName)
    {
        var format = GetFormatDefinition(formatName);
        if (format == null)
            return formatName;
        return format.IsBuiltIn ? $"{format.Name} (built-in)" : format.Name;
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
            return false;

        EmitSignal(SignalName.TileRemoved, id);
        return true;
    }

    public IEnumerable<EditableTile> GetTilesForBiome(string biome)
    {
        return _tiles.Values.Where(tile =>
            tile.Biomes.Count == 0
            || tile.Biomes.Contains(biome, StringComparer.OrdinalIgnoreCase));
    }

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
            return false;

        EmitSignal(SignalName.BiomeRemoved, id);
        return true;
    }

    /// <summary>
    /// Compiles tiles into a single atlas texture with coordinate mapping.
    /// Called automatically after SaveTiles() unless compileAtlas=false.
    /// </summary>
    public TileAtlasCompilationResult CompileAtlas()
    {
        if (_tileSet == null)
            return new TileAtlasCompilationResult(
                false,
                "No TileSet loaded - cannot compile atlas");

        var compiler = new TileAtlasCompiler();
        return compiler.CompileAtlas(this);
    }
}
#endif
