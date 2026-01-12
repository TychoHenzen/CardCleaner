using System.Linq;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;
using IrregularMeshNs = CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

namespace CardCleaner.Tests.Features.Worldgen.IrregularMesh;

/// <summary>
/// Tests for IrregularMeshFogOfWar.
/// Verifies fog state tracking, visibility updates, and events.
/// </summary>
[TestSuite]
public class IrregularMeshFogOfWarTest
{
    private IrregularMeshNs.IrregularMesh _testMesh = null!;
    private IrregularMeshNs.IrregularMeshMapData _mapData = null!;
    private IrregularMeshNs.IrregularMeshFogOfWar _fogOfWar = null!;

    [BeforeTest]
    public void Setup()
    {
        _testMesh = CreateSimpleTestMesh();
        _mapData = new IrregularMeshNs.IrregularMeshMapData(_testMesh);
        _fogOfWar = new IrregularMeshNs.IrregularMeshFogOfWar(_mapData);
    }

    [TestCase]
    public void TestAllCellsStartHidden()
    {
        for (int i = 0; i < _mapData.CellCount; i++)
        {
            AssertThat(_fogOfWar.GetFogState(i)).IsEqual(IrregularMeshNs.FogState.Hidden);
        }
    }

    [TestCase]
    public void TestIsVisibleReturnsFalseInitially()
    {
        AssertBool(_fogOfWar.IsVisible(0)).IsFalse();
    }

    [TestCase]
    public void TestHasBeenSeenReturnsFalseInitially()
    {
        AssertBool(_fogOfWar.HasBeenSeen(0)).IsFalse();
    }

    [TestCase]
    public void TestUpdateVisibilityMarksCurrentCellVisible()
    {
        _fogOfWar.VisionRange = 10f;
        _fogOfWar.UpdateVisibility(0);

        AssertThat(_fogOfWar.GetFogState(0)).IsEqual(IrregularMeshNs.FogState.Visible);
        AssertBool(_fogOfWar.IsVisible(0)).IsTrue();
    }

    [TestCase]
    public void TestUpdateVisibilityMarksCellsInRangeVisible()
    {
        _fogOfWar.VisionRange = 100f; // Large enough to see all cells
        _fogOfWar.UpdateVisibility(0);

        // All 4 cells should be visible
        var visibleCount = 0;
        for (int i = 0; i < _mapData.CellCount; i++)
        {
            if (_fogOfWar.IsVisible(i))
                visibleCount++;
        }

        AssertThat(visibleCount).IsEqual(4);
    }

    [TestCase]
    public void TestCurrentlyVisibleCellsUpdates()
    {
        _fogOfWar.VisionRange = 100f;
        _fogOfWar.UpdateVisibility(0);

        var visible = _fogOfWar.CurrentlyVisibleCells;
        AssertThat(visible.Count).IsEqual(4);
    }

    [TestCase]
    public void TestPreviouslyVisibleCellsBecomeRevealed()
    {
        _fogOfWar.VisionRange = 20f; // See only nearby cells

        // First update - cell 0 sees some cells
        _fogOfWar.UpdateVisibility(0);
        var initiallyVisible = _fogOfWar.CurrentlyVisibleCells.ToHashSet();

        // Second update from different position
        _fogOfWar.VisionRange = 1f; // Very short range
        _fogOfWar.UpdateVisibility(3);

        // Previously visible cells should now be Revealed (not Hidden)
        foreach (var cellId in initiallyVisible)
        {
            var state = _fogOfWar.GetFogState(cellId);
            AssertThat(state).IsNotEqual(IrregularMeshNs.FogState.Hidden);
        }
    }

    [TestCase]
    public void TestSeenCellsIncludesRevealedAndVisible()
    {
        _fogOfWar.VisionRange = 100f;
        _fogOfWar.UpdateVisibility(0);

        var seen = _fogOfWar.SeenCells;
        AssertThat(seen.Count).IsEqual(4);
    }

    [TestCase]
    public void TestRevealCellMarksAsRevealed()
    {
        _fogOfWar.RevealCell(2);

        AssertThat(_fogOfWar.GetFogState(2)).IsEqual(IrregularMeshNs.FogState.Revealed);
        AssertBool(_fogOfWar.HasBeenSeen(2)).IsTrue();
    }

    [TestCase]
    public void TestRevealAllMarksAllCellsRevealed()
    {
        _fogOfWar.RevealAll();

        for (int i = 0; i < _mapData.CellCount; i++)
        {
            AssertBool(_fogOfWar.HasBeenSeen(i)).IsTrue();
        }
    }

    [TestCase]
    public void TestResetClearsAllVisibility()
    {
        _fogOfWar.VisionRange = 100f;
        _fogOfWar.UpdateVisibility(0);
        _fogOfWar.Reset();

        for (int i = 0; i < _mapData.CellCount; i++)
        {
            AssertThat(_fogOfWar.GetFogState(i)).IsEqual(IrregularMeshNs.FogState.Hidden);
        }
    }

    [TestCase]
    public void TestVisionRangeCanBeSet()
    {
        _fogOfWar.VisionRange = 5f;
        AssertThat(_fogOfWar.VisionRange).IsEqual(5f);
    }

    [TestCase]
    public void TestVisionRangeCannotBeNegative()
    {
        _fogOfWar.VisionRange = -10f;
        AssertThat(_fogOfWar.VisionRange).IsEqual(0f);
    }

    [TestCase]
    public void TestVisibilityChangedEventRaised()
    {
        int eventCallCount = 0;
        _fogOfWar.VisibilityChanged += (_) => eventCallCount++;

        _fogOfWar.VisionRange = 100f;
        _fogOfWar.UpdateVisibility(0);

        AssertThat(eventCallCount).IsGreater(0);
    }

    [TestCase]
    public void TestCellRevealedEventRaised()
    {
        int revealedCellId = -1;
        _fogOfWar.CellRevealed += (cellId) => revealedCellId = cellId;

        _fogOfWar.RevealCell(2);

        AssertThat(revealedCellId).IsEqual(2);
    }

    [TestCase]
    public void TestCellRevealedEventNotRaisedForAlreadySeenCell()
    {
        _fogOfWar.RevealCell(2);

        int callCount = 0;
        _fogOfWar.CellRevealed += (_) => callCount++;

        _fogOfWar.RevealCell(2);

        AssertThat(callCount).IsEqual(0);
    }

    [TestCase]
    public void TestGetStatisticsReturnsCorrectCounts()
    {
        _fogOfWar.VisionRange = 100f;
        _fogOfWar.UpdateVisibility(0);

        var (hidden, revealed, visible) = _fogOfWar.GetStatistics();

        AssertThat(hidden).IsEqual(0);
        AssertThat(revealed).IsEqual(0);
        AssertThat(visible).IsEqual(4);
    }

    [TestCase]
    public void TestGetCellsWithStateReturnsCorrectCells()
    {
        _fogOfWar.VisionRange = 100f;
        _fogOfWar.UpdateVisibility(0);

        var visibleCells = _fogOfWar.GetCellsWithState(IrregularMeshNs.FogState.Visible).ToList();

        AssertThat(visibleCells.Count).IsEqual(4);
    }

    [TestCase]
    public void TestCellsPerUnitAffectsVisionRange()
    {
        _fogOfWar.VisionRange = 1f;
        _fogOfWar.CellsPerUnit = 100f; // Effectively 100 units of vision
        _fogOfWar.UpdateVisibility(0);

        // Should see all cells
        AssertThat(_fogOfWar.CurrentlyVisibleCells.Count).IsEqual(4);
    }

    /// <summary>
    /// Create a simple 2x2 quad test mesh with 9 vertices.
    /// </summary>
    private static IrregularMeshNs.IrregularMesh CreateSimpleTestMesh()
    {
        var mesh = new IrregularMeshNs.IrregularMesh();

        // Create 3x3 grid of vertices
        var positions = new[]
        {
            new Vector2(0, 0), new Vector2(16, 0), new Vector2(32, 0),
            new Vector2(0, 16), new Vector2(16, 16), new Vector2(32, 16),
            new Vector2(0, 32), new Vector2(16, 32), new Vector2(32, 32)
        };

        foreach (var pos in positions)
        {
            var id = mesh.AddVertex(pos);
            mesh.Vertices[id].TerrainType = 1;
        }

        // Add 4 quads
        mesh.AddQuad(new[] { 0, 1, 4, 3 });
        mesh.AddQuad(new[] { 1, 2, 5, 4 });
        mesh.AddQuad(new[] { 3, 4, 7, 6 });
        mesh.AddQuad(new[] { 4, 5, 8, 7 });

        mesh.UpdateAllCachedProperties();
        mesh.BuildAdjacency();

        return mesh;
    }
}
