using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CardCleaner.Tests.Architecture.DependencyGate;

/// <summary>
/// The printed report of a measurement (#240 AC-1, AC-2). Two measurements are equal when their lines are.
/// </summary>
internal static class GateReport
{
    public static List<string> Lines(GateMeasurement m)
    {
        var names = m.Modules.Names;
        var gated = m.Gated;
        var lines = new List<string>
        {
            $"nodes {m.Graph.Nodes.Count}, edges {m.Graph.Edges.Count}",
            $"classes: data {m.LayerCount(Layer.Data)}, contracts {m.LayerCount(Layer.Contracts)}, " +
            $"middle {m.LayerCount(Layer.Middle)}, ui {m.LayerCount(Layer.UI)}",
            $"modules {m.RealModuleCount}, loose {m.Modules.Loose.Count}",
            "gated: " + string.Join(", ", GatedNumbers.Fields.Select(f => $"{f.Name} {f.Value(gated)}")),
            $"seams: defined {m.SeamCount(SeamKind.Defined)}, leaky {gated.SeamsLeaky}, " +
            $"missing {gated.SeamsMissing}",
        };

        var sizes = m.Modules.Module.GroupBy(p => p.Value).ToDictionary(g => g.Key, g => g.Count());
        var position = 0;
        foreach (var module in m.Order.Order.Where(sizes.ContainsKey))
            lines.Add($"order {++position}: {names[module]} ({sizes[module]} classes)");
        lines.Add("loose: " + string.Join(", ", m.Modules.Loose));
        foreach (var key in m.Order.UpEdges)
        {
            var edge = m.SpreadEdges[key];
            lines.Add($"up edge: {names[key.From]} -> {names[key.To]} weight {edge.Weight}: " +
                      string.Join(", ", edge.Pairs.Select(p => $"{p.From} -> {p.To} {p.Weight}")));
        }

        foreach (var loop in m.Order.Loops)
            lines.Add($"loop of {loop.Count}: " + string.Join(", ", loop.Select(x => names[x])));
        foreach (var seam in m.Seams.Where(s => s.Rating != SeamKind.Defined))
        {
            lines.Add($"seam {Label(seam.Rating)}: {names[seam.Modules.From]} -> {names[seam.Modules.To]}, top three " +
                      seam.TopThreeShare.ToString("0.00", CultureInfo.InvariantCulture) + ", concrete: " +
                      string.Join(", ", seam.Concrete) + (seam.ConcreteBack ? " (concrete back)" : ""));
        }

        foreach (var component in m.Order.GreedyComponents)
        {
            lines.Add($"greedy order for {component.Count} modules: " +
                      string.Join(", ", component.Select(x => names[x])));
        }

        return lines;
    }

    private static string Label(SeamKind kind) => kind switch
    {
        SeamKind.Defined => "defined",
        SeamKind.Leaky => "leaky",
        _ => "missing",
    };
}
