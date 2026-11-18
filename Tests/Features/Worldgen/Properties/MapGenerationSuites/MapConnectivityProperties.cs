using System.Linq;
using CardCleaner.Tests.Features.Worldgen.Support;

namespace CardCleaner.Tests.Features.Worldgen.Properties.MapGenerationSuites;

/// <summary>
///     Property-based tests for map connectivity: passable tiles, player start and enemy placement (ST008).
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public partial class MapConnectivityProperties : MapGenerationPropertyBase
{
    [TestCase]
    public void Map_AllPassableTilesAreConnected()
    {
        MapProperty(8, 15, 50, request => MapInvariants.AllPassableTilesConnected(GenerateMap(request)));
    }

    [TestCase]
    public void Map_PlayerStartIsOnPassableTile()
    {
        MapProperty(8, 15, 50, request =>
        {
            var map = GenerateMap(request);
            return map.PassableTiles.Contains(map.PlayerStart);
        });
    }

    [TestCase]
    public void Map_EnemyPositionsAreOnPassableTiles()
    {
        MapProperty(10, 15, 50, request =>
        {
            var map = GenerateMap(request);
            return map.EnemyPositions.All(pos => map.PassableTiles.Contains(pos));
        });
    }

    [TestCase]
    public void Map_EnemyPositionsAreDistinctFromPlayerStart()
    {
        MapProperty(10, 15, 50, request =>
        {
            var map = GenerateMap(request);
            return !map.EnemyPositions.Contains(map.PlayerStart);
        });
    }

    [TestCase]
    public void Map_HasAtLeastOnePassableTile()
    {
        MapProperty(5, 10, 50, request => GenerateMap(request).PassableTiles.Count > 0);
    }

    [TestCase]
    public void Map_DimensionsMatchRequested()
    {
        MapProperty(5, 20, 50, request =>
        {
            var map = GenerateMap(request);
            return map.Size == request.Size &&
                   map.TileIds.GetLength(0) == request.Height &&
                   map.TileIds.GetLength(1) == request.Width;
        });
    }
}
