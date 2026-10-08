using System.Linq;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Tests.Features.Card.Models;

[TestSuite]
[RequireGodotRuntime]
public class CardEffectRegionSegmenterTest
{
    private const int Size = CardEffectRegionSegmenter.MaxDimension;

    private static CardEffectRegionMap Segment(byte[] rgba, int width = Size, int height = Size)
    {
        return CardEffectRegionSegmenter.Segment(rgba, width, height);
    }

    private static CardEffectRegionMap SegmentFill(Color color)
    {
        return Segment(CardEffectArtPainter.Fill(Size, Size, color));
    }

    // Red art with a blue patch at x 10..(10 + patchWidth - 1), y 10..11.
    private static CardEffectRegionMap SegmentBluePatchOnRed(int patchWidth)
    {
        var art = CardEffectArtPainter.Paint(Size, Size, (x, y) =>
            x >= 10 && x < 10 + patchWidth && y is 10 or 11 ? Colors.Blue : Colors.Red);
        return Segment(art);
    }

    // Left half red, right half the given colour.
    private static CardEffectRegionMap SegmentRedBeside(Color right)
    {
        return Segment(CardEffectArtPainter.TwoHalves(Size, Size, Colors.Red, right));
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void OneColourImageIsOneRegionCoveringEveryPixel()
    {
        var map = SegmentFill(Colors.Red);

        AssertThat(map.RegionCount).IsEqual(1);
        AssertThat(map.Labels.All(label => label == 0)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TwoColourHalvesAreTwoRegionsThatPartitionTheImage()
    {
        var map = SegmentRedBeside(Colors.Blue);

        AssertThat(map.RegionCount).IsEqual(2);
        AssertThat(map.Labels[0]).IsNotEqual(map.Labels[Size - 1]);
        AssertThat(map.Labels.All(label => label >= 0)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ColoursWithinTheThresholdMergeAndColoursBeyondItDoNot()
    {
        // 250 vs 255 on red is a distance of about 0.02, well under the 0.1 colour threshold.
        var close = Color.Color8(250, 5, 0);
        var far = Color.Color8(200, 0, 0);

        AssertThat(SegmentRedBeside(close).RegionCount).IsEqual(1);
        AssertThat(SegmentRedBeside(far).RegionCount).IsEqual(2);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void PixelsBelowTheAlphaThresholdBelongToNoRegion()
    {
        var map = SegmentRedBeside(Color.Color8(255, 0, 0, 127));

        AssertThat(map.RegionCount).IsEqual(1);
        AssertThat(map.Labels[0]).IsEqual(0);
        AssertThat(map.Labels[Size - 1]).IsEqual(-1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void FullyTransparentArtHasNoRegions()
    {
        var map = SegmentFill(Colors.Transparent);

        AssertThat(map.RegionCount).IsEqual(0);
        AssertThat(map.Labels.All(label => label == -1)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ASpeckSmallerThanTheMergeLimitIsAbsorbedByItsNeighbour()
    {
        // A 2 x 2 blue speck is 4 pixels, under the merge limit of 5.
        AssertThat(SegmentBluePatchOnRed(2).RegionCount).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ASpeckOfTheMergeLimitOrMorePixelsSurvivesAsItsOwnRegion()
    {
        // A 3 x 2 blue patch is 6 pixels, not under the merge limit of 5.
        AssertThat(SegmentBluePatchOnRed(3).RegionCount).IsEqual(2);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ALargeImageIsScaledDownSoItsLongestSideIs128Pixels()
    {
        var map = Segment(CardEffectArtPainter.Fill(256, 192, Colors.Red), 256, 192);

        AssertThat(map.Width).IsEqual(128);
        AssertThat(map.Height).IsEqual(96);
        AssertThat(map.Labels.Length).IsEqual(128 * 96);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ASmallImageIsScaledUpSoItsLongestSideIs128Pixels()
    {
        var map = Segment(CardEffectArtPainter.Fill(16, 32, Colors.Red), 16, 32);

        AssertThat(map.Width).IsEqual(64);
        AssertThat(map.Height).IsEqual(128);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void SegmentingTheSameImageTwiceGivesTheSameLabels()
    {
        var art = CardEffectArtPainter.Paint(Size, Size, (x, y) => (x + y) % 40 < 20 ? Colors.Red : Colors.Blue);

        var first = Segment(art);
        var second = Segment(art);

        AssertThat(first.Labels.SequenceEqual(second.Labels)).IsTrue();
        AssertThat(first.RegionCount).IsEqual(second.RegionCount);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void LabelsAreCompactSoEveryIdBelowRegionCountIsUsed()
    {
        var map = SegmentBluePatchOnRed(3);

        var used = map.Labels.Where(label => label >= 0).Distinct().OrderBy(label => label).ToArray();

        AssertThat(used.SequenceEqual(Enumerable.Range(0, map.RegionCount))).IsTrue();
    }
}
