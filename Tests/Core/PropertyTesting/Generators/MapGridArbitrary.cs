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
    public static Gen<(string[,] TileIds, Vector2I Size)> Small =>
        CreateMapGrid(5, 10, 5, 10);

    /// <summary>
    ///     Generator for medium map grids (10-20 tiles per dimension).
    /// </summary>
    public static Gen<(string[,] TileIds, Vector2I Size)> Medium =>
        CreateMapGrid(10, 20, 10, 20);

    /// <summary>
    ///     Generator for map grids with custom size bounds.
    /// </summary>
    public static Gen<(string[,] TileIds, Vector2I Size)> CreateMapGrid(
        int minWidth, int maxWidth,
        int minHeight, int maxHeight) =>
        from width in Gen.Choose(minWidth, maxWidth)
        from height in Gen.Choose(minHeight, maxHeight)
        from tiles in CreateTileArray(width, height)
        select (tiles, new Vector2I(width, height));

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
        public static Arbitrary<(string[,], Vector2I)> MapGrid() => Arb.From(Small);
    }
}

/// <summary>
///     Helper generators for specific map grid scenarios.
/// </summary>
public static class MapGridGenerators
{
    /// <summary>
    ///     Generator for uniform grids where all tiles are the same type.
    /// </summary>
    public static Gen<(string[,] TileIds, Vector2I Size)> SingleTileType(string tileId) =>
        from width in Gen.Choose(5, 10)
        from height in Gen.Choose(5, 10)
        select CreateUniformGrid(tileId, width, height);

    /// <summary>
    ///     Generator for a map grid paired with a test position within bounds.
    /// </summary>
    public static Gen<(string[,] TileIds, Vector2I Size, Vector2I TestPos)> WithTestPosition =>
        from mapData in MapGridArbitrary.Small
        from testPos in TilePositionArbitrary.InMap(mapData.Size)
        select (mapData.TileIds, mapData.Size, testPos);

    /// <summary>
    ///     Generator for map grids where one tile type dominates by percentage.
    /// </summary>
    public static Gen<(string[,] TileIds, Vector2I Size)> WithDominantTile(string dominantTile, float percentage) =>
        from width in Gen.Choose(5, 10)
        from height in Gen.Choose(5, 10)
        from tiles in CreateDominantTileArray(dominantTile, percentage, width, height)
        select (tiles, new Vector2I(width, height));

    private static (string[,], Vector2I) CreateUniformGrid(string tileId, int width, int height)
    {
        var grid = new string[height, width];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
            grid[y, x] = tileId;
        return (grid, new Vector2I(width, height));
    }

    private static Gen<string[,]> CreateDominantTileArray(string dominantTile, float percentage, int width, int height)
    {
        var otherTiles = new[] { "grass", "dirt", "stone", "water" }.Where(t => t != dominantTile).ToArray();
        // Use Gen.Choose to select tiles based on percentage probability
        var tileGen =
            from chance in Gen.Choose(0, 99)
            from otherTile in Gen.Elements(otherTiles)
            select chance < (int)(percentage * 100) ? dominantTile : otherTile;
        return from tileIds in Gen.ArrayOf(width * height, tileGen)
            select MapGridArbitrary.ToGrid(tileIds, width, height);
    }
}
