using CardCleaner.Scripts.Core.Interfaces;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services;

/// <summary>
/// A generated grid map: the tiles for drawing, and the grid behind the session's cell ids and world positions.
/// </summary>
public interface IGridGeneratedMap : IGeneratedMap
{
    /// <summary>The tiles, the player start and the enemies, for rendering.</summary>
    SimpleMapData RawMapData { get; }

    /// <summary>The grid that converts the session's cell ids and world positions to tiles.</summary>
    RegularGridMapData GridMapData { get; }
}
