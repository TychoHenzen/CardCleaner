namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// What a single collapse step of the solver produced.
/// </summary>
internal readonly record struct WfcStepOutcome(WfcStepStatus Status, WfcSolveResult Failure)
{
    internal static WfcStepOutcome Collapsed => new(WfcStepStatus.Collapsed, default);

    internal static WfcStepOutcome Complete => new(WfcStepStatus.Complete, default);

    internal static WfcStepOutcome Failed(WfcSolveResult failure) => new(WfcStepStatus.Failed, failure);
}
