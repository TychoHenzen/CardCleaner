namespace CardCleaner.Scripts.Features.Worldgen.AutoTiling;

/// <summary>
///     Auto-tile format determining how neighbor matching works.
/// </summary>
public enum AutoTileFormat
{
    /// <summary>
    ///     4-bit corner format: NE=1, SE=2, SW=4, NW=8.
    ///     Checks diagonal neighbors only. 16 possible combinations.
    /// </summary>
    Corner16 = 0,

    /// <summary>
    ///     8-bit blob format: N=1, NE=2, E=4, SE=8, S=16, SW=32, W=64, NW=128.
    ///     Corners only valid when both adjacent edges are set. 47 valid combinations.
    /// </summary>
    Blob47 = 1
}
