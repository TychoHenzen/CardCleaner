using System;
using CardCleaner.Scripts.Core.Services;

namespace CardCleaner.Tests.Core.Services;

/// <summary>
/// Each cache owner registers its clear with AssemblyUnloadCleanup, and unloading runs every registered clear; a clear
/// that throws does not stop the others. The rules are checked on a list of the test's own, so the game's caches (the
/// compiled atlas TileSet, which takes seconds to build) stay filled for the suites after this one.
/// </summary>
[TestSuite]
public class AssemblyUnloadCleanupTest
{
    [TestCase]
    public void ARegisteredClearRunsWhenTheListRuns()
    {
        var clears = new AssemblyUnloadCleanup.ClearList();
        var runs = 0;
        clears.Register(() => runs++);

        clears.RunAll();

        AssertInt(runs).IsEqual(1);
    }

    [TestCase]
    public void AClearThatThrowsDoesNotStopTheNextOne()
    {
        var clears = new AssemblyUnloadCleanup.ClearList();
        var ranAfterThrow = false;
        clears.Register(() => throw new InvalidOperationException("clear failed"));
        clears.Register(() => ranAfterThrow = true);

        clears.RunAll();

        AssertBool(ranAfterThrow).IsTrue();
    }
}
