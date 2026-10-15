using System;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// Per-step time totals for one WFC solve or one generation.
/// </summary>
public readonly record struct WfcStepTimings(
    TimeSpan Propagation,
    TimeSpan TileSelection,
    TimeSpan CellCollapse,
    TimeSpan Solve,
    TimeSpan MapDataConversion)
{
    public static WfcStepTimings Zero => default;

    public static WfcStepTimings operator +(WfcStepTimings left, WfcStepTimings right) => new(
        left.Propagation + right.Propagation,
        left.TileSelection + right.TileSelection,
        left.CellCollapse + right.CellCollapse,
        left.Solve + right.Solve,
        left.MapDataConversion + right.MapDataConversion);
}
