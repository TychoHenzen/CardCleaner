using System;

namespace CardCleaner.Scripts.Features.Worldgen.AutoTiling;

/// <summary>
///     Cardinal neighbor bits for the 4-bit format: N=1, E=2, S=4, W=8.
/// </summary>
[Flags]
public enum Neighbor4 : int
{
    North = 1,
    East = 2,
    South = 4,
    West = 8
}
