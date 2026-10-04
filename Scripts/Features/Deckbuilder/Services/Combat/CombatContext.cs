namespace CardCleaner.Scripts.Features.Deckbuilder.Services.Combat;

/// <summary>
/// Context object passed to combat commands containing the combatants
/// and any state needed for execution and undo.
/// </summary>
public class CombatContext
{
    public required ICombatant Source { get; init; }
    public required ICombatant Target { get; init; }
}
