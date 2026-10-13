using System.Diagnostics;
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
    [TestCategory("Unit")]
    public static void LayeredStructureStaysWithinTheBaseline()
    {
        var clock = Stopwatch.StartNew();
        var root = ProjectSettings.GlobalizePath("res://");
        var first = Measure(root, clock, 1);
        var second = Measure(root, clock, 2);
        var firstReport = string.Join("\n", GateReport.Lines(first));
        foreach (var line in GateReport.Lines(first)) GD.Print("dependency gate: " + line);
        AssertThat(string.Join("\n", GateReport.Lines(second))).IsEqual(firstReport);

        var update = OS.GetEnvironment(DependencyBaseline.SwitchVariable) == "1";
        var failures = DependencyBaseline.Apply(ProjectSettings.GlobalizePath(BaselinePath), first.Gated, update);
        GD.Print($"dependency gate: done in {clock.ElapsedMilliseconds} ms" + (update ? " (baseline update)" : ""));
        AssertThat(string.Join("\n", failures)).IsEmpty();
    }

    private static GateMeasurement Measure(string root, Stopwatch clock, int run)
    {
        var scripts = RepositorySources.Scripts(root);
        var addons = RepositorySources.Addons(root);
        var references = CompilationReferences.FromHost();
        GD.Print($"dependency gate: run {run} read {scripts.Count} scripts, {addons.Count} addon files, " +
                 $"{references.Count} references at {clock.ElapsedMilliseconds} ms");
        var extracted = ClassGraphExtractor.Extract(scripts, addons, references);
        var links = SceneLinkReader.Read(RepositorySources.Scenes(root), extracted.ClassesByFile);
        foreach (var link in links) extracted.Graph.AddEdge(link.From, link.To, EdgeKind.MemberType, link.Count);
        GD.Print($"dependency gate: run {run} extracted ({extracted.ScriptErrors} script errors, {links.Count} scene " +
                 $"links) at {clock.ElapsedMilliseconds} ms");
        var measurement = GateMetrics.Measure(extracted.Graph);
        GD.Print($"dependency gate: run {run} measured at {clock.ElapsedMilliseconds} ms");
        return measurement;
    }
}
