using CardCleaner.Scripts.Features.Worldgen.Wfc;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.Wfc.CellReservationScenarios;

/// <summary>
///     CellReservationBasicTest scenarios split out of CellReservationTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CellReservationBasicTest
{
    // ==================== Initial State ====================

    /// <summary>
    /// Test that newly created cells have no reservation.
    /// </summary>
    [TestCase]
    public void TestNewCell_HasNoReservation()
    {
        var cell = new WfcCellState(new[] { "grass", "dirt" });

        AssertThat(cell.ReservedBy).IsNull();
        AssertBool(cell.IsReserved).IsFalse();
    }

    /// <summary>
    /// Test that IsExcludedFromSelection is false for uncollapsed, unreserved cell.
    /// </summary>
    [TestCase]
    public void TestNewCell_NotExcludedFromSelection()
    {
        var cell = new WfcCellState(new[] { "grass", "dirt" });

        AssertBool(cell.IsExcludedFromSelection()).IsFalse();
    }

    // ==================== Reserve Operation ====================

    /// <summary>
    /// Test Reserve sets ReservedBy to the given position.
    /// </summary>
    [TestCase]
    public void TestReserve_SetsReservedBy()
    {
        var cell = new WfcCellState(new[] { "grass", "dirt" });
        var anchorPosition = new Vector2I(5, 10);

        cell.Reserve(anchorPosition);

        AssertThat(cell.ReservedBy).IsNotNull();
        AssertThat(cell.ReservedBy!.Value).IsEqual(anchorPosition);
    }

    /// <summary>
    /// Test IsReserved returns true after Reserve().
    /// </summary>
    [TestCase]
    public void TestReserve_IsReservedBecomesTrue()
    {
        var cell = new WfcCellState(new[] { "grass", "dirt" });

        cell.Reserve(new Vector2I(3, 4));

        AssertBool(cell.IsReserved).IsTrue();
    }

    /// <summary>
    /// Test IsExcludedFromSelection returns true for reserved cell.
    /// </summary>
    [TestCase]
    public void TestReserve_ExcludesFromSelection()
    {
        var cell = new WfcCellState(new[] { "grass", "dirt" });

        cell.Reserve(new Vector2I(0, 0));

        AssertBool(cell.IsExcludedFromSelection()).IsTrue();
    }

    /// <summary>
    /// Test that reserving with origin position works.
    /// </summary>
    [TestCase]
    public void TestReserve_WithOriginPosition()
    {
        var cell = new WfcCellState(new[] { "grass" });

        cell.Reserve(Vector2I.Zero);

        AssertBool(cell.IsReserved).IsTrue();
        AssertThat(cell.ReservedBy!.Value).IsEqual(Vector2I.Zero);
    }

    /// <summary>
    /// Test that reserving with negative coordinates works.
    /// </summary>
    [TestCase]
    public void TestReserve_WithNegativeCoordinates()
    {
        var cell = new WfcCellState(new[] { "grass" });

        cell.Reserve(new Vector2I(-5, -10));

        AssertBool(cell.IsReserved).IsTrue();
        AssertThat(cell.ReservedBy!.Value.X).IsEqual(-5);
        AssertThat(cell.ReservedBy!.Value.Y).IsEqual(-10);
    }

    /// <summary>
    /// Test that reserving twice overwrites the first reservation.
    /// </summary>
    [TestCase]
    public void TestReserve_OverwritesPreviousReservation()
    {
        var cell = new WfcCellState(new[] { "grass", "dirt" });

        cell.Reserve(new Vector2I(1, 1));
        cell.Reserve(new Vector2I(2, 2));

        AssertThat(cell.ReservedBy!.Value).IsEqual(new Vector2I(2, 2));
    }

    // ==================== ClearReservation Operation ====================

    /// <summary>
    /// Test ClearReservation resets ReservedBy to null.
    /// </summary>
    [TestCase]
    public void TestClearReservation_ResetsToNull()
    {
        var cell = new WfcCellState(new[] { "grass", "dirt" });
        cell.Reserve(new Vector2I(5, 5));

        cell.ClearReservation();

        AssertThat(cell.ReservedBy).IsNull();
    }

    /// <summary>
    /// Test IsReserved returns false after ClearReservation.
    /// </summary>
    [TestCase]
    public void TestClearReservation_IsReservedBecomesFalse()
    {
        var cell = new WfcCellState(new[] { "grass", "dirt" });
        cell.Reserve(new Vector2I(5, 5));

        cell.ClearReservation();

        AssertBool(cell.IsReserved).IsFalse();
    }
}
