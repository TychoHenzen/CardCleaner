using System.Collections.Generic;
using CardCleaner.Scripts.Features.Worldgen.WeightModifiers;
using Godot;
using Godot.Collections;

namespace CardCleaner.Scripts.Features.Worldgen.Structures;

/// <summary>
/// Fixed tile pattern resource representing a pre-defined structure (well, shrine, ruin, etc.).
/// Stamps are placed as a unit and influence nearby tile selection via tile affinities.
/// </summary>
[Tool]
[GlobalClass]
public partial class StructureStamp : Resource
{
    // Default values
    private const string DefaultId = "";
    private const float DefaultSpawnWeight = 1.0f;
    private const int DefaultMinSpacing = 5;
    private const int DefaultInfluenceRadius = 3;
    private static readonly Vector2I DefaultSize = new(1, 1);

    private System.Collections.Generic.Dictionary<Vector2I, string>? _tileCache;
    private bool _cacheInvalid = true;

    public StructureStamp() { }

    /// <summary>Unique identifier for this structure type.</summary>
    [Export] public string Id { get; set; } = DefaultId;

    /// <summary>Size of the structure in tiles (width x height).</summary>
    [Export] public Vector2I Size { get; set; } = DefaultSize;

    /// <summary>Tile entries defining the structure layout.</summary>
    [Export] public Array<StructureTileEntry> Tiles { get; set; } = [];

    /// <summary>Biomes where this structure can spawn. Empty means all biomes allowed.</summary>
    [Export] public Array<string> AllowedBiomes { get; set; } = [];

    /// <summary>Relative spawn weight (higher = more likely to be selected).</summary>
    [Export(PropertyHint.Range, "0,10,0.1")]
    public float SpawnWeight { get; set; } = DefaultSpawnWeight;

    /// <summary>Minimum distance (in tiles) from other structures.</summary>
    [Export(PropertyHint.Range, "0,50,1")]
    public int MinSpacing { get; set; } = DefaultMinSpacing;

    /// <summary>Radius of tile weight influence around the structure.</summary>
    [Export(PropertyHint.Range, "0,20,1")]
    public int InfluenceRadius { get; set; } = DefaultInfluenceRadius;

    /// <summary>Tile weight modifiers applied within the influence radius.</summary>
    [Export] public Array<TileAffinityEntry> TileAffinities { get; set; } = [];

    /// <summary>
    /// Get the tile ID at a specific offset from the anchor.
    /// </summary>
    /// <returns>Tile ID if defined at offset, null otherwise.</returns>
    public string? GetTileAt(Vector2I offset)
    {
        EnsureCacheValid();
        return _tileCache!.TryGetValue(offset, out var tileId) ? tileId : null;
    }

    /// <summary>
    /// Get all tile positions and their IDs.
    /// </summary>
    public IReadOnlyDictionary<Vector2I, string> GetAllTiles()
    {
        EnsureCacheValid();
        return _tileCache!;
    }

    /// <summary>
    /// Check if a biome is allowed for this structure.
    /// </summary>
    public bool IsBiomeAllowed(string biomeId)
    {
        // Empty list means all biomes allowed
        if (AllowedBiomes.Count == 0)
            return true;

        foreach (var allowed in AllowedBiomes)
        {
            if (allowed == biomeId)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Get tile affinities as a dictionary for efficient lookup.
    /// </summary>
    public System.Collections.Generic.Dictionary<string, float> GetAffinityMap()
    {
        var map = new System.Collections.Generic.Dictionary<string, float>();
        foreach (var entry in TileAffinities)
        {
            if (!string.IsNullOrEmpty(entry.TileId))
                map[entry.TileId] = entry.Affinity;
        }
        return map;
    }

    /// <summary>
    /// Check if a position is within this structure's footprint.
    /// </summary>
    public bool ContainsOffset(Vector2I offset)
    {
        return offset.X >= 0 && offset.X < Size.X &&
               offset.Y >= 0 && offset.Y < Size.Y;
    }

    /// <summary>
    /// Mark the tile cache as invalid (call after modifying Tiles).
    /// </summary>
    public void MarkDirty() => _cacheInvalid = true;

    private void EnsureCacheValid()
    {
        if (!_cacheInvalid && _tileCache != null)
            return;

        _tileCache = new System.Collections.Generic.Dictionary<Vector2I, string>();
        foreach (var entry in Tiles)
        {
            if (!string.IsNullOrEmpty(entry.TileId))
                _tileCache[entry.Offset] = entry.TileId;
        }
        _cacheInvalid = false;
    }

    public override bool _PropertyCanRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(Id) => true,
            nameof(Size) => true,
            nameof(SpawnWeight) => true,
            nameof(MinSpacing) => true,
            nameof(InfluenceRadius) => true,
            _ => base._PropertyCanRevert(property)
        };
    }

    public override Variant _PropertyGetRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(Id) => DefaultId,
            nameof(Size) => DefaultSize,
            nameof(SpawnWeight) => DefaultSpawnWeight,
            nameof(MinSpacing) => DefaultMinSpacing,
            nameof(InfluenceRadius) => DefaultInfluenceRadius,
            _ => base._PropertyGetRevert(property)
        };
    }
}
