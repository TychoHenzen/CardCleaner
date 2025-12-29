using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services.Exploration;

/// <summary>
/// Exploration strategy that targets the nearest frontier tile.
/// Uses blob-based prioritization to explore efficiently.
/// </summary>
public class FrontierExplorationStrategy : IExplorationStrategy
{
    /// <inheritdoc />
    public Vector2I? GetNextTarget(ExplorationContext context)
    {
        return context.FrontierBehavior.FindNearestFrontierTile(context.CurrentPosition);
    }
}
