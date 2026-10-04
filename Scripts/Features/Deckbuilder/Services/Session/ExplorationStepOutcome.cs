namespace CardCleaner.Scripts.Features.Deckbuilder.Services.Session;

/// <summary>
/// What the session should do after one exploration step.
/// </summary>
internal enum ExplorationStepOutcome
{
    Continuing,
    EnemyFound,
    Completed
}
