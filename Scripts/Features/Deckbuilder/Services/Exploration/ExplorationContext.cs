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
