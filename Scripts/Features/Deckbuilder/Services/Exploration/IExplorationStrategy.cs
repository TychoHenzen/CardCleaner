using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services.Exploration;

/// <summary>
/// Context object passed to exploration strategies containing all information
/// needed to determine the next exploration target.
/// </summary>
public class ExplorationContext
{
    public required SimpleMapData MapData { get; init; }
    public required Vector2I CurrentPosition { get; init; }
    public required FrontierExplorationBehavior FrontierBehavior { get; init; }
    public Vector2I? VisibleEnemyPosition { get; init; }
    public Vector2I? LastKnownEnemyPosition { get; init; }
}

/// <summary>
/// Strategy interface for determining exploration behavior.
/// Implementations define how the AI selects its next target tile.
/// </summary>
public interface IExplorationStrategy
{
    /// <summary>
    /// Determine the next target tile to move towards.
    /// </summary>
    /// <param name="context">The exploration context with current state.</param>
    /// <returns>The target tile to path to, or null if no valid target exists.</returns>
    Vector2I? GetNextTarget(ExplorationContext context);
}
