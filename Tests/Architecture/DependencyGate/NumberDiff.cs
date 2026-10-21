namespace CardCleaner.Tests.Architecture.DependencyGate;

/// <summary>
/// One gated number that differs from its baseline, and the failure message the ratchet prints for it.
/// </summary>
internal sealed record NumberDiff(string Name, int Baseline, int Measured)
{
    public bool IsHigher => Measured > Baseline;

    /// <summary>
    /// The failure text for this number. The optional detail is the joined and left classes of the list that explains
    /// it; it goes before the lowering instruction of a fall, so the instruction stays the last thing printed.
    /// </summary>
    public string Message(string? detail = null)
    {
        var suffix = DetailSuffix(detail);
        return IsHigher
            ? $"{Name} is {Measured}, above the baseline {Baseline}{suffix}"
            : $"{Name} is {Measured}, below the baseline {Baseline}{suffix}: lower the baseline with " +
              DependencyBaseline.UpdateCommand;
    }

    private static string DetailSuffix(string? detail) => detail is null ? string.Empty : "; " + detail;

    /// <summary>
    /// The message for a rise the decision accepts because a higher-priority number went down (#299).
    /// </summary>
    public string AcceptedRiseMessage(string decidingName, string? detail = null)
    {
        return $"{Name} is {Measured}, above the baseline {Baseline}: accepted because {decidingName} went down" +
               DetailSuffix(detail);
    }
}
