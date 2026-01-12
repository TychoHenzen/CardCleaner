namespace CardCleaner.Scripts.Features.Deckbuilder.Services.Exploration;

/// <summary>
/// Exploration strategy that targets the nearest frontier cell.
/// Uses blob-based prioritization to explore efficiently.
/// </summary>
public class FrontierExplorationStrategy : IExplorationStrategy
{
    /// <inheritdoc />
    public int? GetNextTarget(ExplorationContext context)
    {
        return context.FrontierBehavior.FindNearestFrontierCell(context.CurrentCellId);
    }
}
