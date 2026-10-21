namespace CardCleaner.Tests.Architecture.DependencyGate;

/// <summary>
/// The numbers read from a baseline file, or the failure that names why the file could not be read. Lists is null when
/// the file holds none of the four class-name lists (a numbers-only file).
/// </summary>
internal sealed record BaselineReadResult(GatedNumbers? Numbers, string? Failure, NamedLists? Lists);
