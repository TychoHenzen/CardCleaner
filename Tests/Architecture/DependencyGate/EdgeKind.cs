namespace CardCleaner.Tests.Architecture.DependencyGate;

/// <summary>
/// How a reference site uses its target. The weight is the reference's strength (#240 requirement 2).
/// </summary>
internal enum EdgeKind
{
    Inherits,
    MemberType,
    Creates,
    Calls,
    Uses,
}
