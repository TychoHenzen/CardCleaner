namespace CardCleaner.Scripts.Core.Interfaces;

using Godot;
using System.Collections.Generic;

/// <summary>
/// Read-only view of a generated tile grid: tile and biome ids, terrain overlays, the player start and the
/// enemies, as the grid renderers and the world map read them.
/// </summary>
public interface IGridMap
{
    /// <summary>The grid dimensions (width, height) in tiles.</summary>
    Vector2I Size { get; }

    /// <summary>The biome id of every tile, indexed [y, x], or null when the map has no biome data.</summary>
    string[,]? BiomeMap { get; }

    /// <summary>The background terrain layer (WFC phase 1), indexed [y, x], or null when the map has none.</summary>
    string[,]? BackgroundLayer { get; }

    /// <summary>The foreground auto-tile layer (WFC phase 2), indexed [y, x], or null when the map has none.</summary>
    string[,]? ForegroundLayer { get; }

    /// <summary>Dual-grid terrain data: visual position to base and top tile ids and Corner16 bitmask.</summary>
    IReadOnlyDictionary<Vector2I, (string BaseTileId, string TopTileId, int Bitmask)> DecorationOverlays { get; }

    /// <summary>The tile the player starts on.</summary>
    Vector2I PlayerStart { get; }

    /// <summary>The tiles the enemies stand on.</summary>
    IReadOnlyList<Vector2I> EnemyPositions { get; }

    /// <summary>The tile id at a position, or an empty string outside the grid.</summary>
    string GetTileId(Vector2I pos);

    /// <summary>The biome id at a position.</summary>
    string GetBiomeAt(Vector2I pos);

    /// <summary>Whether a position can be walked through; false outside the grid.</summary>
    bool IsPassable(Vector2I pos);

    /// <summary>Whether a position lets line of sight through; false outside the grid.</summary>
    bool IsTransparent(Vector2I pos);
}
