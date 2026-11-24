using System.Collections.Generic;

namespace CardCleaner.Tests.Architecture.DependencyGate;

/// <summary>
/// The UI modules (#484): the module of each clustered UI class ("U&lt;n&gt;"), the UI classes in no cluster, the layer
/// of every UI module key (all UI), and the display name of every key.
/// </summary>
internal sealed record UiModuleAssignment(
    SortedDictionary<string, string> Module,
    SortedSet<string> Loose,
    SortedDictionary<string, Layer> ModuleLayer,
    SortedDictionary<string, string> Names)
{
    /// <summary>The UI module of a class, its own loose module, or null when the class is not UI.</summary>
    public string? SpreadModule(string id) =>
        Module.TryGetValue(id, out var module) ? module : Loose.Contains(id) ? ModuleAssignment.LooseKey(id) : null;
}
