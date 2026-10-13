namespace CardCleaner.Tests.Architecture.DependencyGate;

/// <summary>The numbers read from a baseline file, or the failure that names why the file could not be read.</summary>
internal sealed record BaselineReadResult(GatedNumbers? Numbers, string? Failure);
