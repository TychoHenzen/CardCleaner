using CardCleaner.Scripts.Features.Worldgen.Wfc;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Contracts;

internal readonly record struct WfcSolveAttempt(
    WfcSolveResult Result,
    IWfcTopology Topology);
