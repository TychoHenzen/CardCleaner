using System;
using System.Globalization;
using System.Text.Json;

namespace CardCleaner.Scripts.Features.SaveSlots.Models;

/// <summary>
///     The versioned wrapper around one slot's payload. The payload is opaque text here; the save format owns its
///     meaning.
/// </summary>
public sealed record SaveEnvelope
{
    public const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };

    public SaveEnvelope(int version, string savedAtUtc, string payload)
    {
        Version = version;
        SavedAtUtc = savedAtUtc;
        Payload = payload;
    }

    public int Version { get; init; }

    public string SavedAtUtc { get; init; }

    public string Payload { get; init; }

    /// <summary>
    ///     Reads an envelope. Refusals carry one of the reasons "empty", "unreadable", "newer version N" or
    ///     "unsupported version N".
    /// </summary>
    public static SaveEnvelopeReadResult Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return SaveEnvelopeReadResult.Refused("empty");
        }

        Wire? wire;
        try
        {
            wire = JsonSerializer.Deserialize<Wire>(json, ReadOptions);
        }
        catch (JsonException)
        {
            return SaveEnvelopeReadResult.Refused("unreadable");
        }

        if (wire?.Version is not { } version || wire.Payload is not { } payload)
        {
            return SaveEnvelopeReadResult.Refused("unreadable");
        }

        if (version > CurrentVersion)
        {
            return SaveEnvelopeReadResult.Refused($"newer version {version}");
        }

        if (version < 1)
        {
            return SaveEnvelopeReadResult.Refused($"unsupported version {version}");
        }

        // ASSUMPTION: a missing SavedAtUtc is read as empty rather than refused, because only the version and the
        // payload decide whether the save can be loaded; the slot list then sorts that slot by name.
        return SaveEnvelopeReadResult.Ok(new SaveEnvelope(version, wire.SavedAtUtc ?? string.Empty, payload));
    }

    /// <summary>
    ///     Builds the JSON text for a payload saved at the given UTC instant, at the current version.
    /// </summary>
    public static string Write(string payload, DateTime utcNow)
    {
        var savedAt = utcNow.ToString("O", CultureInfo.InvariantCulture);
        return JsonSerializer.Serialize(new Wire(CurrentVersion, savedAt, payload));
    }

    private sealed record Wire(int? Version, string? SavedAtUtc, string? Payload);
}
