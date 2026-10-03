using System;
using System.Collections.Generic;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// Dimensions and neighbor geometry of a rectangular WFC grid.
/// Cell IDs are linearized as (y * width + x) for flat iteration.
/// </summary>
internal sealed class WfcGridGeometry
{
    // Pre-allocated neighbor offsets for avoiding repeated allocation in hot paths
    private static readonly Vector2I[] Neighbors4Offsets =
    {
        new(0, -1),  // North
        new(1, 0),   // East
        new(0, 1),   // South
        new(-1, 0)   // West
    };

    private static readonly Vector2I[] Neighbors8Offsets =
    {
        new(0, -1),   // North
        new(1, -1),   // North-East
        new(1, 0),    // East
        new(1, 1),    // South-East
        new(0, 1),    // South
        new(-1, 1),   // South-West
        new(-1, 0),   // West
        new(-1, -1)   // North-West
    };

    internal WfcGridGeometry(int width, int height)
    {
        Width = width;
        Height = height;
    }

    internal int Width { get; }

    internal int Height { get; }

    internal int CellCount => Width * Height;

    internal bool IsInBounds(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;

    internal IEnumerable<Vector2I> EnumerateNeighbors4(int x, int y) => Enumerate(Neighbors4Offsets, x, y);

    internal IEnumerable<Vector2I> EnumerateNeighbors8(int x, int y) => Enumerate(Neighbors8Offsets, x, y);

    internal int CollectNeighbors4(Vector2I pos, Span<Vector2I> output) => Collect(Neighbors4Offsets, pos, output);

    internal int CollectNeighbors8(Vector2I pos, Span<Vector2I> output) => Collect(Neighbors8Offsets, pos, output);

    internal Vector2I CellIdToPosition(int cellId) => new(cellId % Width, cellId / Width);

    internal int PositionToCellId(Vector2I position) => position.Y * Width + position.X;

    private IEnumerable<Vector2I> Enumerate(Vector2I[] offsets, int x, int y)
    {
        foreach (var offset in offsets)
        {
            var nx = x + offset.X;
            var ny = y + offset.Y;
            if (IsInBounds(nx, ny))
                yield return new Vector2I(nx, ny);
        }
    }

    private int Collect(Vector2I[] offsets, Vector2I pos, Span<Vector2I> output)
    {
        var count = 0;

        foreach (var offset in offsets)
        {
            var nx = pos.X + offset.X;
            var ny = pos.Y + offset.Y;
            if (IsInBounds(nx, ny) && count < output.Length)
                output[count++] = new Vector2I(nx, ny);
        }

        return count;
    }
}
