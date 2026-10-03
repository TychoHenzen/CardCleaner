using System.Linq;
using FsCheck;
using Godot;

namespace CardCleaner.Tests.Core.PropertyTesting.Generators;

/// <summary>
///     FsCheck generators for 2D tile ID grids used in auto-tiling and map generation tests.
/// </summary>
public static class MapGridArbitrary
{
    private static readonly string[] DefaultTileIds = { "grass", "dirt", "stone", "water", "sand" };

    /// <summary>
    ///     Generator for small map grids (5-10 tiles per dimension).
    /// </summary>
    public static Gen<MapGridSample> Small =>
        CreateMapGrid(5, 10, 5, 10);

    /// <summary>
    ///     Generator for medium map grids (10-20 tiles per dimension).
    /// </summary>
    public static Gen<MapGridSample> Medium =>
        CreateMapGrid(10, 20, 10, 20);

    /// <summary>
    ///     Generator for map grids with custom size bounds.
    /// </summary>
    public static Gen<MapGridSample> CreateMapGrid(
        int minWidth, int maxWidth,
        int minHeight, int maxHeight) =>
        from width in Gen.Choose(minWidth, maxWidth)
        from height in Gen.Choose(minHeight, maxHeight)
        from tiles in CreateTileArray(width, height)
        select new MapGridSample(tiles, new Vector2I(width, height));

    private static Gen<string[,]> CreateTileArray(int width, int height)
    {
        var tileIdGen = Gen.Elements(DefaultTileIds.Concat(new[] { (string?)null }).ToArray());
        return from tileIds in Gen.ArrayOf(width * height, tileIdGen)
            select ToGrid(tileIds!, width, height);
    }

    /// <summary>
    ///     Convert a flat array of tile IDs to a 2D grid indexed [y, x].
    /// </summary>
    public static string[,] ToGrid(string?[] tileIds, int width, int height)
    {
        var grid = new string[height, width];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var index = y * width + x;
            grid[y, x] = tileIds[index] ?? "";
        }
        return grid;
    }

    public static void Register() => Arb.Register<MapGridArbitraryProvider>();

    private sealed class MapGridArbitraryProvider
    {
        public static Arbitrary<MapGridSample> MapGrid() => Arb.From(Small);
    }
}
