using System.Collections.Generic;

namespace CardCleaner.Tests.Architecture.DependencyGate;

/// <summary>
/// The layer of every class, the real module of every non-loose class, the loose middle classes, and the layer and
/// display name of every module key (loose classes included as "loose &lt;class&gt;").
/// </summary>
internal sealed record ModuleAssignment(
    SortedDictionary<string, Layer> Layers,
    SortedDictionary<string, string> Module,
    SortedSet<string> Loose,
    SortedDictionary<string, Layer> ModuleLayer,
    SortedDictionary<string, string> Names)
{
    public static string LooseKey(string id) => "loose " + id;

    /// <summary>The module of a class for direction and loops: its real module, or its own loose module.</summary>
    public string SpreadModule(string id) => Module.TryGetValue(id, out var module) ? module : LooseKey(id);
}
