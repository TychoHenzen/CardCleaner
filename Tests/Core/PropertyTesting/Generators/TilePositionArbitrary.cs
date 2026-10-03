using System.Collections.Generic;
using FsCheck;
using Godot;

namespace CardCleaner.Tests.Core.PropertyTesting.Generators;

/// <summary>
///     FsCheck generators for tile positions used in auto-tiling and map generation tests.
/// </summary>
public static class TilePositionArbitrary
{
    /// <summary>
    ///     Generator for unbounded tile positions in range [-1000, 1000].
    /// </summary>
    public static Gen<Vector2I> Unbounded =>
        from x in Gen.Choose(-1000, 1000)
        from y in Gen.Choose(-1000, 1000)
        select new Vector2I(x, y);

    /// <summary>
    ///     Generator for small tile positions in range [-10, 10].
    /// </summary>
    public static Gen<Vector2I> Small =>
        from x in Gen.Choose(-10, 10)
        from y in Gen.Choose(-10, 10)
        select new Vector2I(x, y);

    /// <summary>
    ///     Generator for positions within specific bounds.
    /// </summary>
    public static Gen<Vector2I> Bounded(int minX, int maxX, int minY, int maxY) =>
        from x in Gen.Choose(minX, maxX)
        from y in Gen.Choose(minY, maxY)
        select new Vector2I(x, y);

    /// <summary>
    ///     Generator for positions within a map's bounds [0, size-1].
    /// </summary>
    public static Gen<Vector2I> InMap(Vector2I mapSize) =>
        Bounded(0, mapSize.X - 1, 0, mapSize.Y - 1);

    /// <summary>
    ///     Generator for positions in map interior (excludes edges).
    /// </summary>
    public static Gen<Vector2I> InMapInterior(Vector2I mapSize) =>
        mapSize.X <= 2 || mapSize.Y <= 2
            ? InMap(mapSize)
            : Bounded(1, mapSize.X - 2, 1, mapSize.Y - 2);

    public static Arbitrary<Vector2I> Default =>
        Arb.From(Small, ShrinkToOrigin);

    private static IEnumerable<Vector2I> ShrinkToOrigin(Vector2I pos)
    {
        if (pos == Vector2I.Zero) yield break;

        yield return Vector2I.Zero;

        if (pos.X != 0)
            yield return new Vector2I(0, pos.Y);

        if (pos.Y != 0)
            yield return new Vector2I(pos.X, 0);

        if (pos.X != 0)
            yield return new Vector2I(pos.X / 2, pos.Y);

        if (pos.Y != 0)
            yield return new Vector2I(pos.X, pos.Y / 2);
    }

    public static void Register() => Arb.Register<TilePositionArbitraryProvider>();

    private sealed class TilePositionArbitraryProvider
    {
        public static Arbitrary<Vector2I> TilePosition() => Default;
    }
}
