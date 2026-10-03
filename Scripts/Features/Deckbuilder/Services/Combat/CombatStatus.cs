namespace CardCleaner.Scripts.Features.Deckbuilder.Services.Combat;

/// <summary>
/// Snapshot of both combatants' current and maximum health.
/// </summary>
public readonly record struct CombatStatus(
    int PlayerHealth,
    int PlayerMaxHealth,
    int EnemyHealth,
    int EnemyMaxHealth);
