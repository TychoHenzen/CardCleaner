using Godot;

namespace CardCleaner.Tests.Core.PropertyTesting.Generators;

/// <summary>
///     A generated map grid paired with a test position within its bounds.
/// </summary>
public sealed record MapGridSampleWithPosition(string[,] TileIds, Vector2I Size, Vector2I TestPos);
