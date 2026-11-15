namespace CardCleaner.Scripts.Features.Automation;

/// <summary>A command that cannot run as asked; its message goes back to the client as the error.</summary>
public sealed class AutomationException(string message) : System.Exception(message);
