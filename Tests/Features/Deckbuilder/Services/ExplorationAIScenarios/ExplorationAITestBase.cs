using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Tests.TestUtilities.Fixtures;
using Godot;

namespace CardCleaner.Tests.Features.Deckbuilder.Services.ExplorationAIScenarios;

/// <summary>
///     Shared map helpers for the ExplorationAI scenario suites.
/// </summary>
public abstract class ExplorationAITestBase
{
    // Test-local tile IDs (tests don't depend on specific values, just consistent usage)
    protected const string Floor = "floor";
    protected const string Wall = "wall";

    protected static OpenFloorMap CreateMapWithEnemy()
    {
        var map = OpenFloorMap.Create(5, 5, new Vector2I(0, 0));
        map.MapData.EnemyPositions.Add(new Vector2I(4, 4));
        return map;
    }

    protected static void FillWithWalls(string[,] tileIds)
    {
        for (var y = 0; y < tileIds.GetLength(0); y++)
        for (var x = 0; x < tileIds.GetLength(1); x++)
            tileIds[y, x] = Wall;
    }

    /// <summary>
    /// Helper to convert AI's current cell ID back to grid position for assertions.
    /// </summary>
    protected static Vector2I GetCurrentGridPosition(ExplorationAI ai, RegularGridMapData gridData)
    {
        return gridData.CellIdToPosition(ai.CurrentCellId);
    }
}
