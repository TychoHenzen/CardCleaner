namespace CardCleaner.Scripts.Features.Deckbuilder.Services.Exploration;

/// <summary>
/// Strategy interface for determining exploration behavior.
/// Implementations define how the AI selects its next target cell.
/// </summary>
public interface IExplorationStrategy
{
    /// <summary>Determines the next target cell to move towards.</summary>
    int? GetNextTarget(ExplorationContext context);
}
