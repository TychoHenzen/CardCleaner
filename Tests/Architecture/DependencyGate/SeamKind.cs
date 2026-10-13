namespace CardCleaner.Tests.Architecture.DependencyGate;

/// <summary>How well a module seam is defined (#240 requirement 8).</summary>
internal enum SeamKind
{
    Defined,
    Leaky,
    Missing,
}
