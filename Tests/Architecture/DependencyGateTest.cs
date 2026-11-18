using System.Diagnostics;
using System.Threading.Tasks;
using CardCleaner.Tests.Architecture.DependencyGate;
using Godot;

namespace CardCleaner.Tests.Architecture;

/// <summary>
/// The dependency gate (#240): measures the layered module structure of Scripts/ and ratchets it against
/// Tests/Architecture/dependency-baseline.json. See "Dependency Gate" in CLAUDE.md.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class DependencyGateTest
{
    private const string BaselinePath = "res://Tests/Architecture/dependency-baseline.json";

    [TestCase(Timeout = 170000)]
    public static void LayeredStructureStaysWithinTheBaseline()
    {
        var clock = Stopwatch.StartNew();
        var root = ProjectSettings.GlobalizePath("res://");
        // The first run measures the graph this test run shares with the other architecture suites; the second builds
        // its own on a worker thread at the same time, so the two measurements share no state.
        var secondRun = Task.Run(() => Measure(RepositoryClassGraph.Build(root), clock, 2));
        var first = Measure(RepositoryClassGraph.Shared, clock, 1);
        var second = secondRun.GetAwaiter().GetResult();
        var firstReport = string.Join("\n", GateReport.Lines(first));
        foreach (var line in GateReport.Lines(first)) GD.Print("dependency gate: " + line);
        AssertThat(string.Join("\n", GateReport.Lines(second))).IsEqual(firstReport);

        var update = OS.GetEnvironment(DependencyBaseline.SwitchVariable) == "1";
        var baselinePath = ProjectSettings.GlobalizePath(BaselinePath);
        var result = DependencyBaseline.Apply(baselinePath, first.Gated, first.Lists, update);
        foreach (var rise in result.AcceptedRises) GD.Print("dependency gate: " + rise);
        foreach (var note in result.ListNotes) GD.Print("dependency gate: " + note);
        GD.Print($"dependency gate: done in {clock.ElapsedMilliseconds} ms" + (update ? " (baseline update)" : ""));
        AssertThat(string.Join("\n", result.Failures)).IsEmpty();
    }

    private static GateMeasurement Measure(RepositoryClassGraph.Graph graph, Stopwatch clock, int run)
    {
        GD.Print($"dependency gate: run {run} has the graph of {graph.Scripts} scripts, {graph.AddonFiles} addon " +
                 $"files, {graph.References} references ({graph.ScriptErrors} script errors, {graph.SceneLinks} " +
                 $"scene links) at {clock.ElapsedMilliseconds} ms");
        var measurement = GateMetrics.Measure(graph.Classes);
        GD.Print($"dependency gate: run {run} measured at {clock.ElapsedMilliseconds} ms");
        return measurement;
    }
}
