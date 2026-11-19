using System.Collections.Generic;
using Godot;

namespace CardCleaner.Scripts.Core.Services.CompiledAtlas;

/// <summary>
/// The compiled atlas's tile set and the transition tiles it has not created yet. The transition map names about
/// 25,700 tiles, and Godot sorts an atlas source's tile list again on every created tile, so creating them all up
/// front took about 18 s. A transition tile is created when a cell first shows it (<see cref="AtlasCells.Set" />),
/// so a map pays only for the tiles it draws.
/// </summary>
internal sealed class CompiledAtlasTiles
{
    /// <summary>The id of the atlas source in the tile set.</summary>
    internal const int SourceId = 0;

    private readonly TileSetAtlasSource _source;
    private readonly HashSet<Vector2I> _deferred;

    internal CompiledAtlasTiles(TileSet tileSet, TileSetAtlasSource source, HashSet<Vector2I> deferred)
    {
        TileSet = tileSet;
        TileSetId = tileSet.GetInstanceId();
        _source = source;
        _deferred = deferred;
    }

    internal TileSet TileSet { get; }

    // The tile set's instance id, read once: every painted cell looks its tile set up by id, and reading it from the
    // engine for each cached entry on each cell is an interop call per entry.
    internal ulong TileSetId { get; }

    /// <summary>
    /// Whether a cell may show these atlas coordinates: the tile exists, or it is created when first painted.
    /// </summary>
    internal bool Knows(Vector2I atlasCoords) => _source.HasTile(atlasCoords) || _deferred.Contains(atlasCoords);

    /// <summary>Creates the tile at these atlas coordinates if it is a transition tile not created yet.</summary>
    internal void Ensure(int sourceId, Vector2I atlasCoords)
    {
        if (sourceId == SourceId && _deferred.Remove(atlasCoords))
            _source.CreateTile(atlasCoords);
    }
}
