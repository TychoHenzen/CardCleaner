using System.Collections.Generic;
using CardCleaner.Scripts.Features.Deckbuilder.Services;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services.ExplorationAISupport;

internal sealed class ExplorationState
{
    public HashSet<int> VisitedCells { get; } = new();
    public List<int> PathToTarget { get; } = new();
    public int CurrentCellId { get; set; }
    public bool HasFoundEnemy { get; set; }
    public int? VisibleEnemyCellId { get; set; }
    public ExplorationMode CurrentMode { get; set; } = ExplorationMode.FrontierExploration;
    public int? CurrentTargetCell { get; set; }
    public int? LastKnownEnemyCell { get; set; }
    public int? PendingEnemyCell { get; set; }
}
