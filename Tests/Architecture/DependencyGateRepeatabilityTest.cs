using System.Threading.Tasks;
using CardCleaner.Tests.Architecture.DependencyGate;
using Godot;

namespace CardCleaner.Tests.Architecture;

/// <summary>
/// The dependency gate gives the same report from two graphs built independently and at the same time: the shared
/// graph that DependencyGateTest ratchets, and a fresh one built on a worker thread. A difference means the extraction or
/// the clustering depends on state or order it should not (#278). A Daily suite: the second Roslyn build costs several
/// seconds, so on pull requests DependencyGateTest measures the shared graph once.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
[TestCategory("Daily")]
public class DependencyGateRepeatabilityTest
{
    [TestCase(Timeout = 170000)]
    public static void TwoIndependentGraphsGiveTheSameReport()
    {
        var root = ProjectSettings.GlobalizePath("res://");
        var fresh = Task.Run(() => GateMetrics.Measure(RepositoryClassGraph.Build(root).Classes));
        var shared = GateMetrics.Measure(RepositoryClassGraph.Shared.Classes);

        AssertThat(string.Join("\n", GateReport.Lines(fresh.GetAwaiter().GetResult())))
            .IsEqual(string.Join("\n", GateReport.Lines(shared)));
    }
}
