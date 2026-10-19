using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Core.Services;

[TestSuite]
[RequireGodotRuntime]
public class SafePositionTrackersTest
{
    [TestCase]
    [TestCategory("Unit")]
    public void CreateReturnsAnEmptyTracker()
    {
        ISafePositionTracker tracker = SafePositionTrackers.Create();

        AssertInt(tracker.PositionCount).IsEqual(0);
        AssertObject(tracker.GetLastSafePosition()).IsNull();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CreatedTrackerRecordsAValidPositionAndRejectsAnInvalidOne()
    {
        ISafePositionTracker tracker = SafePositionTrackers.Create();

        AssertBool(tracker.RecordSafePosition(new Vector3(1, 2, 3))).IsTrue();
        AssertInt(tracker.PositionCount).IsEqual(1);

        AssertBool(tracker.RecordSafePosition(new Vector3(float.NaN, 0, 0))).IsFalse();
        AssertInt(tracker.PositionCount).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CreateBuildsANewTrackerOnEveryCall()
    {
        ISafePositionTracker first = SafePositionTrackers.Create();
        ISafePositionTracker second = SafePositionTrackers.Create();

        first.RecordSafePosition(new Vector3(1, 1, 1));

        AssertInt(second.PositionCount).IsEqual(0);
    }
}
