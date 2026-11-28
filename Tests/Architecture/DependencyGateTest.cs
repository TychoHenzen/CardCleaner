using System.Diagnostics;
using CardCleaner.Tests.Architecture.DependencyGate;
using Godot;

namespace CardCleaner.Tests.Architecture;

/// <summary>
/// The dependency gate (#240): measures the layered module structure of Scripts/ and ratchets it against
/// Tests/Architecture/dependency-baseline.json. See docs/DEPENDENCY_GATE.md.
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
        // The graph this test run shares with the other architecture suites. That a second, independent build gives the
        // same report is checked daily by DependencyGateRepeatabilityTest.
        var measurement = Measure(RepositoryClassGraph.Shared, clock);
        foreach (var line in GateReport.Lines(measurement)) GD.Print("dependency gate: " + line);

        var update = OS.GetEnvironment(DependencyBaseline.SwitchVariable) == "1";
        var baselinePath = ProjectSettings.GlobalizePath(BaselinePath);
        var result = DependencyBaseline.Apply(baselinePath, measurement.Gated, measurement.Lists, update);
        foreach (var rise in result.AcceptedRises) GD.Print("dependency gate: " + rise);
        foreach (var note in result.ListNotes) GD.Print("dependency gate: " + note);
        GD.Print($"dependency gate: done in {clock.ElapsedMilliseconds} ms" + (update ? " (baseline update)" : ""));
        AssertThat(string.Join("\n", result.Failures)).IsEmpty();
    }

    private static GateMeasurement Measure(RepositoryClassGraph.Graph graph, Stopwatch clock)
    {
        GD.Print($"dependency gate: graph of {graph.Scripts} scripts, {graph.AddonFiles} addon " +
                 $"files, {graph.References} references ({graph.ScriptErrors} script errors, {graph.SceneLinks} " +
                 $"scene links) at {clock.ElapsedMilliseconds} ms");
        var measurement = GateMetrics.Measure(graph.Classes);
        GD.Print($"dependency gate: measured at {clock.ElapsedMilliseconds} ms");
        return measurement;
    }
}
