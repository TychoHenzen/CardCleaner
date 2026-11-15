using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace CardCleaner.Scripts.Features.Automation;

/// <summary>One command: what it does (shown by <c>help</c>) and the handler that runs it on the main thread.</summary>
public sealed record AutomationCommand(string Name, string Usage, Func<JsonObject, Task<JsonNode?>> Run);
