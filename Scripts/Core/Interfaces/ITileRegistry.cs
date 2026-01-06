using System.Collections.Generic;
using CardCleaner.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using Godot;

namespace CardCleaner.Scripts.Core.Interfaces;

public interface ITileRegistry
{
    /// <summary>
    /// Path to the TileSet resource used by these tiles.
    /// When using compiled atlas, this returns the atlas path.
    /// </summary>
    string TilesetPath { get; }

    /// <summary>
    /// Whether the registry is using a compiled atlas instead of source textures.
    /// </summary>
    bool UsingCompiledAtlas { get; }

    /// <summary>
    /// The compiled TileSet if using atlas mode. Use this instead of loading TilesetPath
    /// when UsingCompiledAtlas is true.
    /// </summary>
    TileSet? CompiledTileSet { get; }

    /// <summary>
    /// Configuration for tileset-level spatial properties (tile size, grid offset).
    /// </summary>
    TilesetConfig TilesetConfig { get; }

    void RegisterTile(TileDefinition tile);
    TileDefinition? GetTile(string id);
    IEnumerable<TileDefinition> GetAllTiles();
    IEnumerable<TileDefinition> GetTilesByBiome(string biomeId);
    void Clear();
}
