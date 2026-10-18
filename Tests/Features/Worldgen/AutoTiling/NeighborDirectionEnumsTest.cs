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
        AssertThat((int)Neighbor8.North).IsEqual(1);
        AssertThat((int)Neighbor8.NorthEast).IsEqual(2);
        AssertThat((int)Neighbor8.East).IsEqual(4);
        AssertThat((int)Neighbor8.SouthEast).IsEqual(8);
        AssertThat((int)Neighbor8.South).IsEqual(16);
        AssertThat((int)Neighbor8.SouthWest).IsEqual(32);
        AssertThat((int)Neighbor8.West).IsEqual(64);
        AssertThat((int)Neighbor8.NorthWest).IsEqual(128);
        AssertThat((int)Neighbor8.AllEdges).IsEqual(85);
        AssertThat((int)Neighbor8.AllCorners).IsEqual(170);
        AssertThat((int)Neighbor8.All).IsEqual(255);
        AssertThat((int)Neighbor8.All).IsEqual(255);
        AssertThat((int)Neighbor8.AllEdges).IsEqual(85);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void NeighborCorner_ValuesEqualTheConstantsTheyReplace()
    {
        AssertThat((int)NeighborCorner.NorthEast).IsEqual(1);
        AssertThat((int)NeighborCorner.SouthEast).IsEqual(2);
        AssertThat((int)NeighborCorner.SouthWest).IsEqual(4);
        AssertThat((int)NeighborCorner.NorthWest).IsEqual(8);
        AssertThat((int)NeighborCorner.NorthWest).IsEqual(8);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void Neighbor4_ValuesEqualTheConstantsTheyReplace()
    {
        AssertThat((int)Neighbor4.North).IsEqual(1);
        AssertThat((int)Neighbor4.East).IsEqual(2);
        AssertThat((int)Neighbor4.South).IsEqual(4);
        AssertThat((int)Neighbor4.West).IsEqual(8);
        AssertThat((int)Neighbor4.West).IsEqual(8);
    }
}
