using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Tiles;

/// <summary>
/// Animation configuration for animated tiles.
/// </summary>
public record TileAnimation(
    Vector2I[] Frames,
    float FrameDuration = 0.2f)
{
    /// <summary>
    /// Frame atlas coordinates. First frame is typically the base AtlasCoords.
    /// </summary>
    public Vector2I[] Frames { get; init; } = Frames;

    /// <summary>
    /// Duration of each frame in seconds.
    /// </summary>
    public float FrameDuration { get; init; } = FrameDuration;

    /// <summary>
    /// Total animation duration in seconds.
    /// </summary>
    public float TotalDuration => Frames.Length * FrameDuration;
}
