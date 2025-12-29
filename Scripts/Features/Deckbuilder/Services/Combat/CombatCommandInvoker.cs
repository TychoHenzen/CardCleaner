using System;
using System.Collections.Generic;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services.Combat;

/// <summary>
/// Invoker class for the Command pattern. Executes combat commands
/// and maintains history for undo functionality.
/// </summary>
public class CombatCommandInvoker
{
    private readonly Stack<(ICombatCommand command, CombatContext context)> _history = new();

    /// <summary>
    /// Number of commands that can be undone.
    /// </summary>
    public int HistoryCount => _history.Count;

    /// <summary>
    /// Event raised when a command is executed.
    /// </summary>
    public event Action<string>? CommandExecuted;

    /// <summary>
    /// Event raised when a command is undone.
    /// </summary>
    public event Action<string>? CommandUndone;

    /// <summary>
    /// Execute a command if it can be executed.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="context">The combat context.</param>
    /// <returns>The log message from execution, or null if command couldn't execute.</returns>
    public string? ExecuteCommand(ICombatCommand command, CombatContext context)
    {
        if (!command.CanExecute(context))
            return null;

        var logMessage = command.Execute(context);
        _history.Push((command, context));
        CommandExecuted?.Invoke(logMessage);
        return logMessage;
    }

    /// <summary>
    /// Undo the last executed command.
    /// </summary>
    /// <returns>True if a command was undone.</returns>
    public bool UndoLastCommand()
    {
        if (_history.Count == 0)
            return false;

        var (command, context) = _history.Pop();
        command.Undo(context);
        CommandUndone?.Invoke($"Undid: {command.Name}");
        return true;
    }

    /// <summary>
    /// Clear the command history.
    /// </summary>
    public void ClearHistory()
    {
        _history.Clear();
    }
}
