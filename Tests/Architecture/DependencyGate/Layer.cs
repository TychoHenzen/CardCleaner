namespace CardCleaner.Tests.Architecture.DependencyGate;

/// <summary>The layers of the gate, in module order: foundation first (#240 requirement 7).</summary>
internal enum Layer
{
    Data,
    Contracts,
    Middle,
    UI,
}
