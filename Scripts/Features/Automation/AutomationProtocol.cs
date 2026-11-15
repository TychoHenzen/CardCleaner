using System.Text.Json;
using System.Text.Json.Nodes;

namespace CardCleaner.Scripts.Features.Automation;

/// <summary>
///     The wire format of the automation agent: one JSON object per line each way. A reply carries the request's
///     <c>id</c> and either <c>"ok": true</c> with a <c>result</c>, or <c>"ok": false</c> with an <c>error</c> text.
/// </summary>
public static class AutomationProtocol
{
    public static AutomationRequest Parse(string line)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(line);
        }
        catch (JsonException e)
        {
            throw new AutomationException($"not JSON: {e.Message}");
        }

        if (node is not JsonObject request)
            throw new AutomationException("a request must be a JSON object");
        var command = request["cmd"]?.GetValueKind() == JsonValueKind.String
            ? request["cmd"]!.GetValue<string>()
            : throw new AutomationException("a request needs a \"cmd\" string");
        var args = request["args"] switch
        {
            null => new JsonObject(),
            JsonObject given => (JsonObject)given.DeepClone(),
            _ => throw new AutomationException("\"args\" must be an object")
        };
        return new AutomationRequest(request["id"]?.DeepClone(), command, args);
    }

    public static string Success(JsonNode? id, JsonNode? result) =>
        new JsonObject { ["id"] = id?.DeepClone(), ["ok"] = true, ["result"] = result }.ToJsonString();

    public static string Failure(JsonNode? id, string error) =>
        new JsonObject { ["id"] = id?.DeepClone(), ["ok"] = false, ["error"] = error }.ToJsonString();
}
