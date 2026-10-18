namespace CardCleaner.Scripts.Core.Interfaces;

/// <summary>
/// What the session should do after one exploration step.
/// </summary>
public enum ExplorationStepOutcome
{
    Continuing,
    EnemyFound,
    Completed
}
