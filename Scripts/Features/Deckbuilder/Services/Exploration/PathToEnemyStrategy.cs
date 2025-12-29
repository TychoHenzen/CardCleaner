using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services.Exploration;

/// <summary>
/// Exploration strategy that targets a visible or last-known enemy position.
/// Prioritizes visible enemies over last-known positions.
/// </summary>
public class PathToEnemyStrategy : IExplorationStrategy
{
    /// <inheritdoc />
    public Vector2I? GetNextTarget(ExplorationContext context)
    {
        // Prioritize visible enemy, fall back to last known position
        return context.VisibleEnemyPosition ?? context.LastKnownEnemyPosition;
    }
}
