using CardCleaner.Scripts.Features.Workshop.Models;
using Godot;

namespace CardCleaner.Tests.Features.Workshop.Models;

/// <summary>
/// Grid coordinates convert between local positions and cells exactly like GridMap does, including negative
/// cells and positions that sit exactly on a cell boundary.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class GridCoordinatesTest
{
    private const float CellEdge = 0.2f;
    private const float Tolerance = 0.0001f;

    private static readonly GridCoordinates Grid = new(new Vector3(CellEdge, CellEdge, CellEdge));

    [TestCase]
    [TestCategory("Unit")]
    public static void OriginBelongsToTheCellOnItsPositiveSide()
    {
        AssertThat(Grid.LocalToCell(Vector3.Zero)).IsEqual(Vector3I.Zero);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void PositionInsideACellMapsToThatCell()
    {
        AssertThat(Grid.LocalToCell(new Vector3(0.45f, 0.05f, 1.01f))).IsEqual(new Vector3I(2, 0, 5));
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void NegativePositionsFloorTowardsNegativeInfinity()
    {
        AssertThat(Grid.LocalToCell(new Vector3(-0.01f, -0.21f, -0.19f))).IsEqual(new Vector3I(-1, -2, -1));
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void CellCentreRoundTripsToTheSameCell()
    {
        foreach (var cell in new[] { Vector3I.Zero, new Vector3I(30, -1, 0), new Vector3I(-7, 3, -250), new Vector3I(340, 0, 110) })
            AssertThat(Grid.LocalToCell(Grid.CellToLocal(cell))).IsEqual(cell);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void CellCentreIsHalfACellAboveItsMinCorner()
    {
        var cell = new Vector3I(-3, 2, 5);

        var offset = Grid.CellToLocal(cell) - Grid.CellMinCorner(cell);

        AssertThat(offset.IsEqualApprox(new Vector3(CellEdge, CellEdge, CellEdge) / 2f)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void MinCornerBelongsToItsOwnCellForBoundariesOfEitherSign()
    {
        for (var i = -40; i <= 40; i++)
        {
            var cell = new Vector3I(i, i, -i);

            AssertThat(Grid.LocalToCell(Grid.CellMinCorner(cell) + new Vector3(Tolerance, Tolerance, Tolerance))).IsEqual(cell);
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void MatchesGridMapForBoundariesAndNegativeCells()
    {
        var gridMap = new GridMap { CellSize = new Vector3(CellEdge, CellEdge, CellEdge) };
        try
        {
            for (var i = -60; i <= 60; i++)
            {
                var onBoundary = new Vector3(i * CellEdge, -i * CellEdge, i * CellEdge * 0.5f);
                var inside = onBoundary + new Vector3(0.07f, -0.03f, 0.09f);

                AssertThat(Grid.LocalToCell(onBoundary)).IsEqual(gridMap.LocalToMap(onBoundary));
                AssertThat(Grid.LocalToCell(inside)).IsEqual(gridMap.LocalToMap(inside));
            }

            foreach (var cell in new[] { new Vector3I(30, -1, 4), new Vector3I(-9, 0, -12) })
                AssertThat(Grid.CellToLocal(cell).IsEqualApprox(gridMap.MapToLocal(cell))).IsTrue();
        }
        finally
        {
            gridMap.Free();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void CellsMayDifferInSizePerAxis()
    {
        var tall = new GridCoordinates(new Vector3(0.2f, 1f, 0.5f));

        AssertThat(tall.LocalToCell(new Vector3(0.3f, 2.5f, -0.1f))).IsEqual(new Vector3I(1, 2, -1));
        AssertThat(tall.CellToLocal(new Vector3I(1, 2, -1)).IsEqualApprox(new Vector3(0.3f, 2.5f, -0.25f))).IsTrue();
    }
}
