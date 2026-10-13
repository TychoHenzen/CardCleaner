using System.Collections.Generic;

namespace CardCleaner.Tests.Architecture.DependencyGate;

/// <summary>
/// The module order (foundation first), the up edges (heaviest first), the loops (strongly connected components of
/// more than one module, biggest first) and the components ordered greedily because they exceed the exact limit.
/// </summary>
internal sealed record ModuleOrderResult(
    List<string> Order,
    List<EdgeKey> UpEdges,
    List<List<string>> Loops,
    List<List<string>> GreedyComponents)
{
    public int BiggestLoop => Loops.Count == 0 ? 1 : Loops[0].Count;
}
