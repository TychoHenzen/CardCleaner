namespace CardCleaner.Scripts.Features.Deckbuilder.Services.Combat;

/// <summary>
/// Command pattern interface for encapsulating combat actions.
/// Commands are executable, undoable, and can validate preconditions.
/// </summary>
public interface ICombatCommand
{
    /// <summary>
    /// The display name of this command.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Human-readable description of what this command does.
    /// </summary>
    string Description { get; }

    /// <summary>Checks whether this command can execute in the current context.</summary>
    bool CanExecute(CombatContext context);

    /// <summary>Executes the command and returns a log message.</summary>
    string Execute(CombatContext context);

    /// <summary>
    /// Undo the command, reversing its effects.
    /// </summary>
    /// <param name="context">The combat context.</param>
    void Undo(CombatContext context);
}
