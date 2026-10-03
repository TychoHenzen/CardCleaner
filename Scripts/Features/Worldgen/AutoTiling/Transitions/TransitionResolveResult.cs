using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.AutoTiling;

/// <summary>
/// Result of resolving a terrain transition.
/// Contains the atlas coordinates and source ID for rendering.
/// </summary>
public record TransitionResolveResult(int SourceId, Vector2I AtlasCoords);
