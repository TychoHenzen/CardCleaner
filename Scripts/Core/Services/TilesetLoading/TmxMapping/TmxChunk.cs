using Godot;

namespace CardCleaner.Scripts.Core.Services.TilesetLoading.TmxMapping;

/// <summary>
/// Represents a chunk of tile data from a TMX infinite map.
/// </summary>
internal sealed class TmxChunk
{
    internal Vector2I ChunkPosition { get; }
    internal int Width { get; }
    internal int Height { get; }
    private readonly int[] _tiles;

    internal TmxChunk(Vector2I chunkPosition, int width, int height, int[] tiles)
    {
        ChunkPosition = chunkPosition;
        Width = width;
        Height = height;
        _tiles = tiles;
    }

    internal int GetTileAt(int localX, int localY)
    {
        if (localX < 0 || localX >= Width || localY < 0 || localY >= Height)
            return 0;
        return _tiles[localY * Width + localX];
    }
}
