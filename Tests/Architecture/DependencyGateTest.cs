using System.Diagnostics;
using System.Linq;
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
    [TestCase(Timeout = 170000)]
    [TestCategory("Unit")]
    public static void ExtractsTheClassGraph()
    {
        var clock = Stopwatch.StartNew();
        var root = ProjectSettings.GlobalizePath("res://");
        var scripts = RepositorySources.Scripts(root);
        var addons = RepositorySources.Addons(root);
        var references = CompilationReferences.FromHost();
        GD.Print($"dependency gate: {scripts.Count} scripts, {addons.Count} addon files, {references.Count} references " +
                 $"read in {clock.ElapsedMilliseconds} ms");
        var extracted = ClassGraphExtractor.Extract(scripts, addons, references);
        GD.Print($"dependency gate: extracted in {clock.ElapsedMilliseconds} ms, script errors {extracted.ScriptErrors}");
        var links = SceneLinkReader.Read(RepositorySources.Scenes(root), extracted.ClassesByFile);
        foreach (var link in links) extracted.Graph.AddEdge(link.From, link.To, EdgeKind.MemberType, link.Count);
        var graph = extracted.Graph;
        GD.Print($"dependency gate: nodes {graph.Nodes.Count}, edges {graph.Edges.Count}, scene links {links.Count}, " +
                 $"ui seeds {graph.Nodes.Values.Count(n => n.UiSeed)}, roles " +
                 string.Join(" ", graph.Nodes.Values.GroupBy(n => n.Role).OrderBy(g => g.Key)
                     .Select(g => $"{g.Key}={g.Count()}")) + $" in {clock.ElapsedMilliseconds} ms");
        AssertInt(graph.Nodes.Count).IsGreater(0);
    }
}
