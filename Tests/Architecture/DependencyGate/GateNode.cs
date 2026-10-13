namespace CardCleaner.Tests.Architecture.DependencyGate;

/// <summary>One class of the graph: a top-level type of Scripts/ (nested types fold into it).</summary>
/// <param name="Id">Unique short name: the type name, or name@namespace-tail when two types share a name.</param>
/// <param name="Role">The role from the declaration (interface, abstract, data, function, static, concrete).</param>
/// <param name="Home">Features/X, Core/X or the first folder under Scripts/.</param>
/// <param name="UiSeed">Derives from GodotObject or names a non-value GodotSharp type (UI rule 1).</param>
internal sealed record GateNode(string Id, NodeRole Role, string Home, bool UiSeed)
{
    /// <summary>A contract role: users depend on its shape only (interface, abstract, data or function).</summary>
    public bool IsContractRole => Role is NodeRole.Interface or NodeRole.Abstract or NodeRole.Data or NodeRole.Function;
}
