using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using CardCleaner.Tests.TestUtilities;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling;

/// <summary>
/// The Neighbor8, NeighborCorner and Neighbor4 flags enums carry the same bit values as the constants they replace.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class NeighborDirectionEnumsTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void Neighbor8_ValuesEqualTheConstantsTheyReplace()
    {
        AssertThat((int)Neighbor8.North).IsEqual(NeighborBitmask8.North);
        AssertThat((int)Neighbor8.NorthEast).IsEqual(NeighborBitmask8.NorthEast);
        AssertThat((int)Neighbor8.East).IsEqual(NeighborBitmask8.East);
        AssertThat((int)Neighbor8.SouthEast).IsEqual(NeighborBitmask8.SouthEast);
        AssertThat((int)Neighbor8.South).IsEqual(NeighborBitmask8.South);
        AssertThat((int)Neighbor8.SouthWest).IsEqual(NeighborBitmask8.SouthWest);
        AssertThat((int)Neighbor8.West).IsEqual(NeighborBitmask8.West);
        AssertThat((int)Neighbor8.NorthWest).IsEqual(NeighborBitmask8.NorthWest);
        AssertThat((int)Neighbor8.AllEdges).IsEqual(NeighborBitmask8.AllEdges);
        AssertThat((int)Neighbor8.AllCorners).IsEqual(NeighborBitmask8.AllCorners);
        AssertThat((int)Neighbor8.All).IsEqual(NeighborBitmask8.All);
        AssertThat((int)Neighbor8.All).IsEqual(255);
        AssertThat((int)Neighbor8.AllEdges).IsEqual(85);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void NeighborCorner_ValuesEqualTheConstantsTheyReplace()
    {
        AssertThat((int)NeighborCorner.NorthEast).IsEqual(NeighborBitmaskCorner.NorthEast);
        AssertThat((int)NeighborCorner.SouthEast).IsEqual(NeighborBitmaskCorner.SouthEast);
        AssertThat((int)NeighborCorner.SouthWest).IsEqual(NeighborBitmaskCorner.SouthWest);
        AssertThat((int)NeighborCorner.NorthWest).IsEqual(NeighborBitmaskCorner.NorthWest);
        AssertThat((int)NeighborCorner.NorthWest).IsEqual(8);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void Neighbor4_ValuesEqualTheConstantsTheyReplace()
    {
        AssertThat((int)Neighbor4.North).IsEqual(NeighborBitmask.North);
        AssertThat((int)Neighbor4.East).IsEqual(NeighborBitmask.East);
        AssertThat((int)Neighbor4.South).IsEqual(NeighborBitmask.South);
        AssertThat((int)Neighbor4.West).IsEqual(NeighborBitmask.West);
        AssertThat((int)Neighbor4.West).IsEqual(8);
    }
}
