using System;
using System.Threading;
using Godot;

namespace CardCleaner.Tests.Architecture.DependencyGate;

/// <summary>
/// The class graph of Scripts/ with its scene links, as the dependency gate measures it. Building one is a Roslyn
/// compilation of every script and addon file, several seconds of the gate's time, so one graph per test run is
/// shared: the gate measures it and SignatureLayerTest reads it. <see cref="Build" /> makes a fresh graph, which the
/// gate measures as its second, independent run. No caller may change a graph it is given.
/// </summary>
internal static class RepositoryClassGraph
{
    private static readonly Lazy<Graph> SharedGraph =
        new(() => Build(ProjectSettings.GlobalizePath("res://")), LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>One built graph and the counts the gate prints about it.</summary>
    internal sealed record Graph(ClassGraph Classes, int Scripts, int AddonFiles, int References, int ScriptErrors,
        int SceneLinks);

    /// <summary>The graph shared by every suite of this test run, built on first use.</summary>
    internal static Graph Shared => SharedGraph.Value;

    /// <summary>A graph built now from the files under <paramref name="projectRoot" />, scene links included.</summary>
    internal static Graph Build(string projectRoot)
    {
        var scripts = RepositorySources.Scripts(projectRoot);
        var addons = RepositorySources.Addons(projectRoot);
        var references = CompilationReferences.FromHost();
        var extracted = ClassGraphExtractor.Extract(scripts, addons, references);
        var links = SceneLinkReader.Read(RepositorySources.Scenes(projectRoot), extracted.ClassesByFile);
        foreach (var link in links) extracted.Graph.AddEdge(link.From, link.To, EdgeKind.MemberType, link.Count);
        return new Graph(extracted.Graph, scripts.Count, addons.Count, references.Count, extracted.ScriptErrors,
            links.Count);
    }
}
