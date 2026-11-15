using System.Text.Json.Nodes;

namespace CardCleaner.Scripts.Features.Automation;

/// <summary>One command line from a client: <c>{"id": 1, "cmd": "walk_to", "args": {...}}</c>.</summary>
public sealed record AutomationRequest(JsonNode? Id, string Command, JsonObject Args);
