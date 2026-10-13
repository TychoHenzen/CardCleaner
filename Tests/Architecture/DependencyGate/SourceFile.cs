namespace CardCleaner.Tests.Architecture.DependencyGate;

/// <summary>One source file: its path relative to the project root ("Scripts/Core/X.cs") and its text.</summary>
internal sealed record SourceFile(string RelativePath, string Text);
