namespace CardCleaner.Scripts.Features.Deckbuilder.Services.Exploration;

/// <summary>
/// Exploration strategy that targets a visible or last-known enemy position.
/// Prioritizes visible enemies over last-known positions.
/// </summary>
public class PathToEnemyStrategy : IExplorationStrategy
{
    /// <inheritdoc />
    public int? GetNextTarget(ExplorationContext context)
    {
        // Prioritize visible enemy, fall back to last known position
        return context.VisibleEnemyCellId ?? context.LastKnownEnemyCellId;
    }
}
