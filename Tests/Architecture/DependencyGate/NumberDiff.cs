namespace CardCleaner.Tests.Architecture.DependencyGate;

/// <summary>
/// One gated number that differs from its baseline, and the failure message the ratchet prints for it.
/// </summary>
internal sealed record NumberDiff(string Name, int Baseline, int Measured)
{
    public bool IsHigher => Measured > Baseline;

    public string Message => IsHigher
        ? $"{Name} is {Measured}, above the baseline {Baseline}"
        : $"{Name} is {Measured}, below the baseline {Baseline}: lower the baseline with " +
          DependencyBaseline.UpdateCommand;
}
