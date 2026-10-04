using System.Threading;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Services;
using GdUnit4;

namespace CardCleaner.Tests.Core.Services;

[TestSuite]
[RequireGodotRuntime]
public class GodotProgressTest
{
    [TestCase]
    public async Task ReportQueuedBeforeCancellationDoesNotEmitAfterNodeIsFreed()
    {
        using var cancellation = new CancellationTokenSource();
        var progress = new GodotProgress(cancellation.Token);
        Assertions.AddNode(progress);
        var updateCount = 0;
        progress.ProgressUpdated += _ => updateCount++;

        progress.Report(0.25f);
        cancellation.Cancel();
        progress.QueueFree();

        await ISceneRunner.SyncProcessFrame;

        progress.EmitProgress(0.5f);
        progress.Report(0.75f);
        await ISceneRunner.SyncProcessFrame;

        Assertions.AssertThat(updateCount).IsEqual(0);
    }
}
