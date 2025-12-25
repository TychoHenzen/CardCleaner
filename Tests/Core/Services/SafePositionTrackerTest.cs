using CardCleaner.Scripts.Core.Services;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Core.Services;

[TestSuite]
[RequireGodotRuntime]
public class SafePositionTrackerTest
{
    private SafePositionTracker _tracker = null!;

    [BeforeTest]
    public void Setup() => _tracker = new SafePositionTracker();

    [TestCase]
    public void TestRecordSafePosition_ValidPosition_ReturnsTrue()
    {
        var position = new Vector3(1, 2, 3);

        var result = _tracker.RecordSafePosition(position);

        AssertBool(result).IsTrue();
        AssertInt(_tracker.PositionCount).IsEqual(1);
    }

    [TestCase]
    public void TestRecordSafePosition_InvalidPosition_ReturnsFalse()
    {
        var position = new Vector3(float.NaN, 0, 0);

        var result = _tracker.RecordSafePosition(position);

        AssertBool(result).IsFalse();
        AssertInt(_tracker.PositionCount).IsEqual(0);
    }

    [TestCase]
    public void TestRecordSafePosition_InfinitePosition_ReturnsFalse()
    {
        var position = new Vector3(float.PositiveInfinity, 0, 0);

        var result = _tracker.RecordSafePosition(position);

        AssertBool(result).IsFalse();
        AssertInt(_tracker.PositionCount).IsEqual(0);
    }

    [TestCase]
    public void TestGetLastSafePosition_NoPositions_ReturnsNull()
    {
        var result = _tracker.GetLastSafePosition();

        AssertObject(result).IsNull();
    }

    [TestCase]
    public void TestGetLastSafePosition_OnePosition_ReturnsIt()
    {
        var position = new Vector3(5, 10, 15);
        _tracker.RecordSafePosition(position);

        var result = _tracker.GetLastSafePosition();

        AssertObject(result).IsNotNull();
        AssertThat(result!.Value).IsEqual(position);
    }

    [TestCase]
    public void TestGetLastSafePosition_MultiplePositions_ReturnsLast()
    {
        var first = new Vector3(1, 1, 1);
        var second = new Vector3(2, 2, 2);
        var third = new Vector3(3, 3, 3);

        _tracker.RecordSafePosition(first);
        _tracker.RecordSafePosition(second);
        _tracker.RecordSafePosition(third);

        var result = _tracker.GetLastSafePosition();

        AssertThat(result!.Value).IsEqual(third);
    }

    [TestCase]
    public void TestBoundedQueue_ExceedsMaxPositions_RemovesOldest()
    {
        // Record more than the max (10)
        for (var i = 0; i < 15; i++) _tracker.RecordSafePosition(new Vector3(i, i, i));

        // Should only have 10 positions
        AssertInt(_tracker.PositionCount).IsEqual(10);

        // Last position should be the 15th one (index 14)
        var last = _tracker.GetLastSafePosition();
        AssertThat(last!.Value).IsEqual(new Vector3(14, 14, 14));
    }

    [TestCase]
    public void TestSetSpawnPosition_ValidPosition_SetsIt()
    {
        var spawn = new Vector3(100, 0, 100);

        _tracker.SetSpawnPosition(spawn);
        var result = _tracker.GetSpawnPosition();

        AssertObject(result).IsNotNull();
        AssertThat(result!.Value).IsEqual(spawn);
    }

    [TestCase]
    public void TestGetSpawnPosition_NotSet_ReturnsNull()
    {
        var result = _tracker.GetSpawnPosition();

        AssertObject(result).IsNull();
    }

    [TestCase]
    public void TestClear_RemovesAllPositions()
    {
        _tracker.RecordSafePosition(new Vector3(1, 1, 1));
        _tracker.RecordSafePosition(new Vector3(2, 2, 2));
        _tracker.SetSpawnPosition(new Vector3(0, 0, 0));

        _tracker.Clear();

        AssertInt(_tracker.PositionCount).IsEqual(0);
        AssertObject(_tracker.GetLastSafePosition()).IsNull();
        AssertObject(_tracker.GetSpawnPosition()).IsNull();
    }
}
