namespace CardCleaner.Scripts.Features.Worldgen.AutoTiling.Validation;

/// <summary>
/// The pair of corner bits (and their display names) that two adjacent visual tiles share.
/// </summary>
internal readonly record struct SharedCorner(int BitA, int BitB, string NameA, string NameB);
