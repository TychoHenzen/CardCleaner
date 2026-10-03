namespace CardCleaner.Features.Worldgen.AutoTiling;

/// <summary>
/// Specifies the neighbor computation algorithm for auto-tile bitmask calculation.
/// This determines how neighboring cells are analyzed to produce a bitmask value.
/// </summary>
public enum BitmaskType
{
    /// <summary>
    /// 4-bit diagonal corner format checking only NE, SE, SW, NW neighbors.
    /// Produces bitmask values 0-15. Maps to <see cref="NeighborBitmaskCorner"/>.
    /// </summary>
    Corner4 = 0,

    /// <summary>
    /// 4-bit cardinal edge format checking only N, E, S, W neighbors.
    /// Produces bitmask values 0-15. Maps to <see cref="NeighborBitmask"/>.
    /// </summary>
    Edge4 = 1,

    /// <summary>8-bit format checking all eight neighbors for Blob47 combinations.</summary>
    Full8 = 2
}
