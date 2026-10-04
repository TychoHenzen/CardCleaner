using System;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Services.Exploration;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services.ExplorationAISupport;

internal sealed class ExplorationStepCallbacks
{
    public required Func<IExplorationStrategy> GetCurrentStrategy { get; init; }
    public required Action<ExplorationMode> SetMode { get; init; }
    public required Action<int> MoveToCell { get; init; }
    public required Action<Vector2> EnemyEncountered { get; init; }
    public required Action PathUpdated { get; init; }
}
