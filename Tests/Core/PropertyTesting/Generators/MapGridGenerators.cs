using System.Linq;
using FsCheck;
using Godot;

namespace CardCleaner.Tests.Core.PropertyTesting.Generators;

/// <summary>
///     Helper generators for specific map grid scenarios.
/// </summary>
public static class MapGridGenerators
{
    /// <summary>
    ///     Generator for uniform grids where all tiles are the same type.
    /// </summary>
    public static Gen<MapGridSample> SingleTileType(string tileId) =>
        from width in Gen.Choose(5, 10)
        from height in Gen.Choose(5, 10)
        select CreateUniformGrid(tileId, width, height);

    /// <summary>
    ///     Generator for a map grid paired with a test position within bounds.
    /// </summary>
    public static Gen<MapGridSampleWithPosition> WithTestPosition =>
        from mapData in MapGridArbitrary.Small
        from testPos in TilePositionArbitrary.InMap(mapData.Size)
        select new MapGridSampleWithPosition(mapData.TileIds, mapData.Size, testPos);

    /// <summary>
    ///     Generator for map grids where one tile type dominates by percentage.
    /// </summary>
    public static Gen<MapGridSample> WithDominantTile(string dominantTile, float percentage) =>
        from width in Gen.Choose(5, 10)
        from height in Gen.Choose(5, 10)
        from tiles in CreateDominantTileArray(dominantTile, percentage, width, height)
        select new MapGridSample(tiles, new Vector2I(width, height));

    private static MapGridSample CreateUniformGrid(string tileId, int width, int height)
    {
        var grid = new string[height, width];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
            grid[y, x] = tileId;
        return new MapGridSample(grid, new Vector2I(width, height));
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
