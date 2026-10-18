using System;

namespace CardCleaner.Scripts.Features.Worldgen.AutoTiling;

/// <summary>Eight-direction blob bits: N=1, NE=2, E=4, SE=8, S=16, SW=32, W=64, NW=128.</summary>
[Flags]
public enum Neighbor8 : int
{
    North = 1,
    NorthEast = 2,
    East = 4,
    SouthEast = 8,
    South = 16,
    SouthWest = 32,
    West = 64,
    NorthWest = 128,
    AllEdges = North | East | South | West,
    AllCorners = NorthEast | SouthEast | SouthWest | NorthWest,
    All = AllEdges | AllCorners
}
