namespace CardCleaner.Scripts.Core.Devices;

/// <summary>
/// Fire-and-forget event data. Carries no payload.
/// </summary>
public readonly struct Trigger
{
    public static readonly Trigger Instance = new();
}
