using System.Collections.Generic;

namespace CardCleaner.Tests.Architecture.DependencyGate;

/// <summary>
/// The UI layer seen from inside (#484): its modules, the references between them and their order. The up edges and
/// the biggest loop of Order are the two UI gated numbers.
/// </summary>
internal sealed record UiView(
    UiModuleAssignment Modules,
    SortedDictionary<EdgeKey, ModuleEdge> Edges,
    ModuleOrderResult Order);
