namespace CardCleaner.Tests.Architecture.DependencyGate;

/// <summary>What a class is, as far as the layer rules care (#240 requirement 3).</summary>
internal enum NodeRole
{
    Interface,
    Abstract,
    Data,
    Function,
    Static,
    Concrete,
}
