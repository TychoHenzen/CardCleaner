using CardCleaner.Scripts.Features.SaveSlots.Models;

namespace CardCleaner.Scripts.Features.SaveSlots.Services;

/// <summary>
///     The outcome of loading one slot: no save file, a loaded envelope, or a refusal with its reason.
/// </summary>
public sealed record LoadResult
{
    private LoadResult(SaveEnvelope? envelope, string? refusal, bool missing)
    {
        Envelope = envelope;
        Refusal = refusal;
        IsMissing = missing;
    }

    public static LoadResult Missing { get; } = new(null, null, true);

    public SaveEnvelope? Envelope { get; }

    public string? Refusal { get; }

    public bool IsMissing { get; }

    public bool IsLoaded => Envelope is not null;

    public static LoadResult Loaded(SaveEnvelope envelope) => new(envelope, null, false);

    public static LoadResult Refused(string reason) => new(null, reason, false);
}
