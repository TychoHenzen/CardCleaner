namespace CardCleaner.Scripts.Features.SaveSlots.Models;

/// <summary>
///     The outcome of reading an envelope: either the envelope, or the reason it was refused.
/// </summary>
public sealed record SaveEnvelopeReadResult
{
    private SaveEnvelopeReadResult(SaveEnvelope? envelope, string? refusal)
    {
        Envelope = envelope;
        Refusal = refusal;
    }

    public SaveEnvelope? Envelope { get; }

    public string? Refusal { get; }

    public bool IsOk => Envelope is not null;

    public static SaveEnvelopeReadResult Ok(SaveEnvelope envelope) => new(envelope, null);

    public static SaveEnvelopeReadResult Refused(string reason) => new(null, reason);
}
