using CardCleaner.Scripts.Features.Worldgen.Wfc;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.Wfc.CellReservationScenarios;

/// <summary>
///     CellReservationClearAndCopyTest scenarios split out of CellReservationTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CellReservationClearAndCopyTest
{
    /// <summary>
    /// Test IsExcludedFromSelection returns false after clearing reservation.
    /// </summary>
    [TestCase]
    public void TestClearReservation_NoLongerExcludedFromSelection()
    {
        var cell = new WfcCellState(new[] { "grass", "dirt" });
        cell.Reserve(new Vector2I(5, 5));

        cell.ClearReservation();

        AssertBool(cell.IsExcludedFromSelection()).IsFalse();
    }

    /// <summary>
    /// Test ClearReservation on unreserved cell is a no-op.
    /// </summary>
    [TestCase]
    public void TestClearReservation_OnUnreservedCell_NoOp()
    {
        var cell = new WfcCellState(new[] { "grass", "dirt" });

        cell.ClearReservation(); // Should not throw

        AssertThat(cell.ReservedBy).IsNull();
        AssertBool(cell.IsReserved).IsFalse();
    }

    // ==================== Interaction with Collapse ====================

    /// <summary>
    /// Test that collapsed cell is excluded from selection.
    /// </summary>
    [TestCase]
    public void TestCollapsedCell_ExcludedFromSelection()
    {
        var cell = new WfcCellState(new[] { "grass", "dirt" });

        cell.CollapseTo("grass");

        AssertBool(cell.IsExcludedFromSelection()).IsTrue();
    }

    /// <summary>
    /// Test that both collapsed AND reserved cell is excluded.
    /// </summary>
    [TestCase]
    public void TestCollapsedAndReserved_ExcludedFromSelection()
    {
        var cell = new WfcCellState(new[] { "grass", "dirt" });

        cell.CollapseTo("grass");
        cell.Reserve(new Vector2I(1, 1));

        AssertBool(cell.IsExcludedFromSelection()).IsTrue();
    }

    /// <summary>
    /// Test reserved but not collapsed cell is still excluded.
    /// </summary>
    [TestCase]
    public void TestReservedNotCollapsed_ExcludedFromSelection()
    {
        var cell = new WfcCellState(new[] { "grass", "dirt", "sand" });

        cell.Reserve(new Vector2I(2, 2));

        AssertBool(cell.IsCollapsed()).IsFalse();
        AssertBool(cell.IsExcludedFromSelection()).IsTrue();
    }

    // ==================== Copy Constructor ====================

    /// <summary>
    /// Test copy constructor preserves reservation state when reserved.
    /// </summary>
    [TestCase]
    public void TestCopyConstructor_PreservesReservation()
    {
        var original = new WfcCellState(new[] { "grass", "dirt" });
        original.Reserve(new Vector2I(7, 8));

        var copy = new WfcCellState(original);

        AssertBool(copy.IsReserved).IsTrue();
        AssertThat(copy.ReservedBy!.Value).IsEqual(new Vector2I(7, 8));
    }

    /// <summary>
    /// Test copy constructor preserves non-reserved state.
    /// </summary>
    [TestCase]
    public void TestCopyConstructor_PreservesNoReservation()
    {
        var original = new WfcCellState(new[] { "grass", "dirt" });

        var copy = new WfcCellState(original);

        AssertBool(copy.IsReserved).IsFalse();
        AssertThat(copy.ReservedBy).IsNull();
    }

    /// <summary>
    /// Test that copy is independent - modifying copy doesn't affect original.
    /// </summary>
    [TestCase]
    public void TestCopyConstructor_IndependentReservation()
    {
        var original = new WfcCellState(new[] { "grass", "dirt" });
        original.Reserve(new Vector2I(1, 1));

        var copy = new WfcCellState(original);
        copy.ClearReservation();

        // Original should still be reserved
        AssertBool(original.IsReserved).IsTrue();
        AssertBool(copy.IsReserved).IsFalse();
    }

    /// <summary>
    /// Test that modifying original reservation doesn't affect copy.
    /// </summary>
    [TestCase]
    public void TestCopyConstructor_OriginalIndependent()
    {
        var original = new WfcCellState(new[] { "grass", "dirt" });
        var copy = new WfcCellState(original);

        original.Reserve(new Vector2I(5, 5));

        AssertBool(original.IsReserved).IsTrue();
        AssertBool(copy.IsReserved).IsFalse();
    }

    // ==================== Edge Cases ====================

    /// <summary>
    /// Test that single-tile cell can be reserved.
    /// </summary>
    [TestCase]
    public void TestSingleTileCell_CanBeReserved()
    {
        var cell = new WfcCellState(new[] { "grass" });

        cell.Reserve(new Vector2I(0, 0));

        AssertBool(cell.IsReserved).IsTrue();
        AssertBool(cell.IsCollapsed()).IsTrue(); // Single tile = collapsed
        AssertBool(cell.IsExcludedFromSelection()).IsTrue();
    }

    /// <summary>
    /// Test reserve/clear cycle works correctly.
    /// </summary>
    [TestCase]
    public void TestReserveClearCycle()
    {
        var cell = new WfcCellState(new[] { "grass", "dirt" });

        // First cycle
        cell.Reserve(new Vector2I(1, 1));
        AssertBool(cell.IsReserved).IsTrue();

        cell.ClearReservation();
        AssertBool(cell.IsReserved).IsFalse();

        // Second cycle with different position
        cell.Reserve(new Vector2I(9, 9));
        AssertBool(cell.IsReserved).IsTrue();
        AssertThat(cell.ReservedBy!.Value).IsEqual(new Vector2I(9, 9));

        cell.ClearReservation();
        AssertBool(cell.IsReserved).IsFalse();
    }
}
