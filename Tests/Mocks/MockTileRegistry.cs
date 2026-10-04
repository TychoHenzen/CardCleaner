using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using Godot;

namespace CardCleaner.Tests.Mocks;

/// <summary>
/// Mock ITileRegistry and ITileMetadataProvider for testing. Allows configuring tiles
/// with specific properties without loading from tiles.json.
/// </summary>
public class MockTileRegistry : ITileRegistry, ITileMetadataProvider
{
    private readonly Dictionary<string, TileDefinition> _tiles = new();
    private readonly VariationGroupCollection _variationGroups = new();

    public string TilesetPath => "res://test.tres";
    public bool UsingCompiledAtlas => false;
    public TileSet? CompiledTileSet => null;
    public TilesetConfig TilesetConfig => TilesetConfig.Default;

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

    // Variation group methods (stub implementations for testing)
    public VariationGroup? GetVariationGroup(string tileId) => _variationGroups.FindGroupContaining(tileId);
    public VariationGroup? GetVariationGroupByBaseName(string baseName) =>
        _variationGroups.GetGroupByBaseName(baseName);

    public bool AreSameTerrainType(string? tileId1, string? tileId2)
    {
        if (string.IsNullOrEmpty(tileId1) && string.IsNullOrEmpty(tileId2))
            return true;
        if (string.IsNullOrEmpty(tileId1) || string.IsNullOrEmpty(tileId2))
            return false;
        if (tileId1 == tileId2)
            return true;

        var group1 = _variationGroups.FindGroupContaining(tileId1);
        var group2 = _variationGroups.FindGroupContaining(tileId2);
        if (group1 != null && group2 != null)
            return group1.BaseName == group2.BaseName;

        var tile1 = GetTile(tileId1);
        var tile2 = GetTile(tileId2);
        if (tile1?.AutoTileVariants != null && tile2?.AutoTileVariants != null)
            return ReferenceEquals(tile1.AutoTileVariants, tile2.AutoTileVariants);

        return false;
    }

    public string? SelectPerMapVariant(string baseName, RandomNumberGenerator rng) => null;
    public IReadOnlyList<VariantWeight> GetInstanceVariants(string baseName) => Array.Empty<VariantWeight>();
    public IEnumerable<VariationGroup> GetAllVariationGroups() => _variationGroups.GetAllGroups();

    /// <summary>
    /// Helper to add a variation group for testing.
    /// </summary>
    public void AddVariationGroup(VariationGroup group) => _variationGroups.AddGroup(group);

    /// <summary>
    /// Helper to create a simple auto-tile (HasAutoTileVariants = true).
    /// </summary>
    public static TileDefinition CreateAutoTile(string id, string name = "")
    {
        // Non-null autoTileVariants array makes HasAutoTileVariants return true
        var variants = new Vector2I?[16];
        return new TileDefinition(
            id,
            string.IsNullOrEmpty(name) ? id : name,
            TilePassability.Passable,
            Vector2I.Zero,
            new TileDefinitionOptions
            {
                AutoTileVariants = variants
            });
    }

    /// <summary>
    /// Helper to create a simple gap/fill tile (HasAutoTileVariants = false).
    /// </summary>
    public static TileDefinition CreateGapTile(string id, string name = "")
    {
        // No autoTileVariants = gap tile
        return new TileDefinition(
            id,
            string.IsNullOrEmpty(name) ? id : name,
            TilePassability.Passable,
            Vector2I.Zero);
    }

    /// <summary>
    /// Creates a registry pre-configured with standard test tiles:
    /// - grass, stone = auto-tiles
    /// - dirt, rock = gap tiles
    /// </summary>
    public static MockTileRegistry CreateWithTestTiles()
    {
        var registry = new MockTileRegistry();
        registry.RegisterTile(CreateAutoTile("grass", "Grass"));
        registry.RegisterTile(CreateAutoTile("stone", "Stone"));
        registry.RegisterTile(CreateGapTile("dirt", "Dirt"));
        registry.RegisterTile(CreateGapTile("rock", "Rock"));
        return registry;
    }

    #region ITileMetadataProvider Implementation

    public IReadOnlyList<TileDefinition> GetSimpleTerrainTiles()
        => _tiles.Values.Where(t => t.IsSimpleTerrain).ToList();

    public IReadOnlyList<TileDefinition> GetAutoTiles()
        => _tiles.Values.Where(t => t.IsAutoTile).ToList();

    public IReadOnlyList<TileDefinition> GetGapTiles()
        => _tiles.Values.Where(t => t.IsGapTile).ToList();

    public IReadOnlyList<TileDefinition> GetPassableTerrainTiles()
        => _tiles.Values.Where(t => t.Layer == TileLayer.Terrain && t.IsPassable).ToList();

    public IReadOnlyList<TileDefinition> GetSolidTerrainTiles()
        => _tiles.Values.Where(t => t.Layer == TileLayer.Terrain && t.IsSolid).ToList();

    public IReadOnlyList<TileDefinition> GetDecorationTiles()
        => _tiles.Values.Where(t => t.IsDecoration).ToList();

    public IReadOnlyList<TileDefinition> GetTilesByLayer(TileLayer layer)
        => _tiles.Values.Where(t => t.Layer == layer).ToList();

    IReadOnlyList<TileDefinition> ITileMetadataProvider.GetTilesByBiome(string biomeId)
        => _tiles.Values.Where(t => t.IsAllowedInBiome(biomeId)).ToList();

    public IReadOnlyList<TileDefinition> GetBackgroundTerrainTiles()
        => _tiles.Values.Where(t => t.IsSimpleTerrain && t.IsPassable).ToList();

    public TileDefinition? GetDefaultPassableTile()
        => _tiles.Values.FirstOrDefault(t => t.IsGapTile && t.IsPassable)
           ?? _tiles.Values.FirstOrDefault(t => t.IsSimpleTerrain && t.IsPassable)
           ?? _tiles.Values.FirstOrDefault(t => t.Layer == TileLayer.Terrain && t.IsPassable);

    public TileDefinition? GetDefaultSolidTile()
        => _tiles.Values.FirstOrDefault(t => t.Layer == TileLayer.Terrain && t.IsSolid);

    public TileDefinition? GetDefaultGapTile()
        => _tiles.Values.FirstOrDefault(t => t.IsGapTile)
           ?? _tiles.Values.FirstOrDefault(t => t.IsSimpleTerrain && t.IsPassable);

    public bool IsAutoTile(string tileId)
        => _tiles.TryGetValue(tileId, out var tile) && tile.IsAutoTile;

    public bool IsPassable(string tileId)
        => _tiles.TryGetValue(tileId, out var tile) && tile.IsPassable;

    public bool IsSolid(string tileId)
        => _tiles.TryGetValue(tileId, out var tile) && tile.IsSolid;

    public bool IsGapTile(string tileId)
        => _tiles.TryGetValue(tileId, out var tile) && tile.IsGapTile;

    public bool IsTransparent(string tileId)
        => _tiles.TryGetValue(tileId, out var tile) && tile.IsTransparent;

    public IReadOnlyList<string> GetWfcTileIds()
        => _tiles.Values.Where(t => t.Layer == TileLayer.Terrain).Select(t => t.Id).ToList();

    public string? GetDefaultPassableTileId() => GetDefaultPassableTile()?.Id;

    public string? GetDefaultSolidTileId() => GetDefaultSolidTile()?.Id;

    public string? GetDefaultGapTileId() => GetDefaultGapTile()?.Id;

    #endregion
}
