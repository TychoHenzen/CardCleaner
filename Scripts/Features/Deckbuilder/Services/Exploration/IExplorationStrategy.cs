using CardCleaner.Scripts.Core.Interfaces;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services.Exploration;

/// <summary>
/// Context object passed to exploration strategies containing all information
/// needed to determine the next exploration target.
/// Now works with cell IDs for grid-agnostic operation.
/// </summary>
public class ExplorationContext
{
    public required IMapData MapData { get; init; }
    public required int CurrentCellId { get; init; }
    public required FrontierExplorationBehavior FrontierBehavior { get; init; }
    public int? VisibleEnemyCellId { get; init; }
    public int? LastKnownEnemyCellId { get; init; }
}

/// <summary>
/// Strategy interface for determining exploration behavior.
/// Implementations define how the AI selects its next target cell.
/// </summary>
public interface IExplorationStrategy
{
    /// <summary>
    /// Determine the next target cell to move towards.
    /// </summary>
    /// <param name="context">The exploration context with current state.</param>
    /// <returns>The target cell ID to path to, or null if no valid target exists.</returns>
    int? GetNextTarget(ExplorationContext context);
}
