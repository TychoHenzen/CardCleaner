using System.Collections.Generic;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;

namespace CardCleaner.Scripts.Core.Interfaces;

public interface ITileRegistry
{
    /// <summary>
    /// Path to the TileSet resource used by these tiles
    /// </summary>
    string TilesetPath { get; }

    void RegisterTile(TileDefinition tile);
    TileDefinition? GetTile(string id);
    IEnumerable<TileDefinition> GetAllTiles();
    IEnumerable<TileDefinition> GetTilesByBiome(BiomeType biome);
    void Clear();
}
