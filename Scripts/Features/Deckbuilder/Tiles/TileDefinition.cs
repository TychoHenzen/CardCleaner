using System.Collections.Generic;
using CardCleaner.Scripts.Core.Enumeration;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Tiles;

/// <summary>
/// Defines a tile type with all its properties
/// </summary>
public class TileDefinition
{
    public TileDefinition(
        string id,
        string name,
        TilePassability passability,
        Vector2I atlasCoords,
        int sourceId = 4,
        TileLayer layer = TileLayer.Terrain,
        float elevation = 0f,
        bool? isTransparent = null,
        HashSet<BiomeType>? allowedBiomes = null)
    {
        Id = id;
        Name = name;
        Passability = passability;
        AtlasCoords = atlasCoords;
        SourceId = sourceId;
        Layer = layer;
        Elevation = elevation;
        IsTransparent = isTransparent ?? (passability == TilePassability.Passable);
        AllowedBiomes = allowedBiomes;
    }

    public string Id { get; }
    public string Name { get; }
    public TilePassability Passability { get; }
    public Vector2I AtlasCoords { get; }
    public int SourceId { get; }
    public TileLayer Layer { get; }
    public float Elevation { get; }
    public bool IsTransparent { get; }
    public HashSet<BiomeType>? AllowedBiomes { get; }

    public bool IsPassable => Passability == TilePassability.Passable;

    public bool IsAllowedInBiome(BiomeType biome) => AllowedBiomes == null || AllowedBiomes.Contains(biome);
}
