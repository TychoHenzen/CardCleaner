using System.Collections.Generic;

namespace CardCleaner.Tests.Architecture.DependencyGate;

/// <summary>
/// All class references from one module to another: summed weight and the class pairs, heaviest first.
/// </summary>
internal sealed class ModuleEdge
{
    public long Weight { get; set; }

    public List<(string From, string To, long Weight)> Pairs { get; } = [];
}
