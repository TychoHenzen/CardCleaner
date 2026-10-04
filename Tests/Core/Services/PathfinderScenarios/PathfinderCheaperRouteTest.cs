using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using Godot;

namespace CardCleaner.Tests.Core.Services.PathfinderScenarios;

/// <summary>
/// A cheaper route to a cell that is already queued must re-prioritise that cell.
/// </summary>
[TestSuite]
public class PathfinderCheaperRouteTest
{
    private const int Start = 0;
    private const int Near = 1;
    private const int Far = 2;
    private const int Goal = 3;

    private Pathfinder _pathfinder = null!;

    [BeforeTest]
    public void Setup() => _pathfinder = new Pathfinder(new FourCellGraph());

    [TestCase]
    public void FindPathPrefersCheaperRouteDiscoveredAfterTheCellWasQueued()
    {
        var path = _pathfinder.FindPath(Start, Goal);

        AssertThat(path).IsEqual(new List<int> { Start, Near, Far, Goal });
    }

    private sealed class FourCellGraph : IMapData
    {
        private static readonly Vector2[] Centers =
        {
            new(0, 0), new(1, 0), new(9, 0), new(10, 0)
        };

        private static readonly Dictionary<(int, int), float> Costs = new()
        {
            [(Start, Near)] = 1f,
            [(Near, Far)] = 1f,
            [(Start, Far)] = 100f,
            [(Far, Goal)] = 1f,
            [(Start, Goal)] = 50f
        };

        public int CellCount => Centers.Length;
        public Rect2 WorldBounds => new(0, 0, 10, 1);
        public bool IsValidCell(int cellId) => cellId >= 0 && cellId < Centers.Length;
        public Vector2 GetCellCenter(int cellId) => Centers[cellId];
        public float GetCellArea(int cellId) => 1f;
        public string GetTerrainType(int cellId) => "floor";
        public bool IsPassable(int cellId) => true;
        public bool IsTransparent(int cellId) => true;
        public bool HasStructure(int cellId) => false;

        public IEnumerable<int> GetAdjacentCells(int cellId)
        {
            foreach (var (from, to) in Costs.Keys)
            {
                if (from == cellId) yield return to;
            }
        }

        public float GetMovementCost(int fromCell, int toCell) =>
            Costs.TryGetValue((fromCell, toCell), out var cost) ? cost : float.PositiveInfinity;

        public int? GetCellAtPosition(Vector2 worldPos) => null;
        public IEnumerable<int> GetCellsInRadius(Vector2 center, float radius) => new List<int>();
        public IEnumerable<int> GetCellsInRect(Rect2 rect) => new List<int>();
        public int? PlayerStartCell => Start;
        public IReadOnlyList<int> EnemySpawnCells => new List<int>();
    }
}
