using CardCleaner.Scripts.Features.Deckbuilder.Services;
using Godot;

namespace CardCleaner.Tests.Features.Deckbuilder.Services.FrontierExplorationScenarios;

/// <summary>
///     Shared tile IDs and map builders for the FrontierExplorationBehavior scenario suites.
/// </summary>
public abstract class FrontierExplorationTestBase
{
    // Test-local tile IDs (tests don't depend on specific values, just consistent usage)
    protected const string Floor = "floor";
    protected const string Wall = "wall";

    /// <summary>
    ///     Builds an L-shaped corridor whose corner tile (5,5) is a small blob hidden around the corner.
    /// </summary>
    protected static SimpleMapData CreateLShapedCorridorMap()
    {
        //   x=0  1  2  3  4  5
        // y=0  F  F  F  F  F  W
        // y=1  W  W  W  W  F  W
        // y=2  W  W  W  W  F  W
        // y=3  W  W  W  W  F  W
        // y=4  W  W  W  W  F  W
        // y=5  W  W  W  W  F  F <- (5,5) is the small blob
        var mapData = new SimpleMapData
        {
            TileIds = new string[6, 6],
            Size = new Vector2I(6, 6),
            PlayerStart = new Vector2I(0, 0)
        };

        FillWithWalls(mapData.TileIds);

        // Top row: (0,0) to (4,0)
        for (var x = 0; x <= 4; x++)
        {
            mapData.TileIds[0, x] = Floor;
            mapData.PassableTiles.Add(new Vector2I(x, 0));
        }

        // Right column: (4,1) to (4,5)
        for (var y = 1; y <= 5; y++)
        {
            mapData.TileIds[y, 4] = Floor;
            mapData.PassableTiles.Add(new Vector2I(4, y));
        }

        // Small blob at corner: (5,5)
        mapData.TileIds[5, 5] = Floor;
        mapData.PassableTiles.Add(new Vector2I(5, 5));
        return mapData;
    }

    protected static void FillWithWalls(string[,] tileIds)
    {
        for (var y = 0; y < tileIds.GetLength(0); y++)
        for (var x = 0; x < tileIds.GetLength(1); x++)
            tileIds[y, x] = Wall;
    }
}
