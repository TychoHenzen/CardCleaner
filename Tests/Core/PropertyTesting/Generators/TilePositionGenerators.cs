using FsCheck;
using Godot;

namespace CardCleaner.Tests.Core.PropertyTesting.Generators;

/// <summary>
///     Helper generators for specific tile position scenarios.
/// </summary>
public static class TilePositionGenerators
{
    /// <summary>
    ///     Generator for a position paired with its containing map size.
    /// </summary>
    public static Gen<(Vector2I Position, Vector2I MapSize)> PositionWithMapSize =>
        from width in Gen.Choose(5, 20)
        from height in Gen.Choose(5, 20)
        let mapSize = new Vector2I(width, height)
        from position in TilePositionArbitrary.InMap(mapSize)
        select (position, mapSize);

    /// <summary>
    ///     Generator for map corner positions.
    /// </summary>
    public static Gen<Vector2I> MapCorner(Vector2I mapSize) =>
        Gen.Elements(
            Vector2I.Zero,
            new Vector2I(mapSize.X - 1, 0),
            new Vector2I(0, mapSize.Y - 1),
            new Vector2I(mapSize.X - 1, mapSize.Y - 1)
        );

    /// <summary>
    ///     Generator for positions on map edges.
    /// </summary>
    public static Gen<Vector2I> MapEdge(Vector2I mapSize) =>
        Gen.OneOf(
            TilePositionArbitrary.Bounded(0, 0, 0, mapSize.Y - 1),
            TilePositionArbitrary.Bounded(mapSize.X - 1, mapSize.X - 1, 0, mapSize.Y - 1),
            TilePositionArbitrary.Bounded(0, mapSize.X - 1, 0, 0),
            TilePositionArbitrary.Bounded(0, mapSize.X - 1, mapSize.Y - 1, mapSize.Y - 1)
        );
}
