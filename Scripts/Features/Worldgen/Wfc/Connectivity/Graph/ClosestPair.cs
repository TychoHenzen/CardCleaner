using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Connectivity;

/// <summary>
/// The nearest two nodes found between a pair of components, with their Manhattan distance.
/// </summary>
internal readonly record struct ClosestPair(Vector2I First, Vector2I Second, int Distance);
