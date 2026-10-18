using System;

namespace CardCleaner.Scripts.Features.Worldgen.AutoTiling;

/// <summary>Diagonal corner bits for the 16-tile corner format: NE=1, SE=2, SW=4, NW=8.</summary>
[Flags]
public enum NeighborCorner : int
{
    NorthEast = 1,
    SouthEast = 2,
    SouthWest = 4,
    NorthWest = 8
}
