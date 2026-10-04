using Godot;

namespace CardCleaner.Tests.Core.PropertyTesting.Generators;

/// <summary>
///     A generated tile ID grid (indexed [y, x]) together with its dimensions.
/// </summary>
public sealed record MapGridSample(string[,] TileIds, Vector2I Size);
