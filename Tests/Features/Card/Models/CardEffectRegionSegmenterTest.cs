using System;
using System.Linq;
using CardCleaner.Scripts.Features.Card.Models;

namespace CardCleaner.Tests.Features.Card.Models;

[TestSuite]
[RequireGodotRuntime]
public class CardEffectRegionSegmenterTest
{
    private const int Size = CardEffectRegionSegmenter.MaxDimension;

    private static readonly (byte R, byte G, byte B, byte A) Red = (255, 0, 0, 255);
    private static readonly (byte R, byte G, byte B, byte A) Blue = (0, 0, 255, 255);
    private static readonly (byte R, byte G, byte B, byte A) Clear = (0, 0, 0, 0);

    [TestCase]
    [TestCategory("Unit")]
    public static void OneColourImageIsOneRegionCoveringEveryPixel()
    {
        var map = CardEffectRegionSegmenter.Segment(CardEffectArtPainter.Paint(Size, Size, (_, _) => Red), Size, Size);

        AssertThat(map.RegionCount).IsEqual(1);
        AssertThat(map.Labels.All(label => label == 0)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TwoColourHalvesAreTwoRegionsThatPartitionTheImage()
    {
        var map = CardEffectRegionSegmenter.Segment(CardEffectArtPainter.Paint(Size, Size, (x, _) => x < Size / 2 ? Red : Blue), Size,
            Size);

        AssertThat(map.RegionCount).IsEqual(2);
        AssertThat(map.Labels[0]).IsNotEqual(map.Labels[Size - 1]);
        AssertThat(map.Labels.All(label => label >= 0)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ColoursWithinTheThresholdMergeAndColoursBeyondItDoNot()
    {
        // 250 vs 255 on red is a distance of about 0.02, well under the 0.1 colour threshold.
        var close = CardEffectArtPainter.Paint(Size, Size, (x, _) => x < Size / 2 ? Red : ((byte)250, (byte)5, (byte)0, (byte)255));
        var far = CardEffectArtPainter.Paint(Size, Size, (x, _) => x < Size / 2 ? Red : ((byte)200, (byte)0, (byte)0, (byte)255));

        AssertThat(CardEffectRegionSegmenter.Segment(close, Size, Size).RegionCount).IsEqual(1);
        AssertThat(CardEffectRegionSegmenter.Segment(far, Size, Size).RegionCount).IsEqual(2);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void PixelsBelowTheAlphaThresholdBelongToNoRegion()
    {
        var map = CardEffectRegionSegmenter.Segment(
            CardEffectArtPainter.Paint(Size, Size, (x, _) => x < Size / 2 ? Red : ((byte)255, (byte)0, (byte)0, (byte)127)), Size, Size);

        AssertThat(map.RegionCount).IsEqual(1);
        AssertThat(map.Labels[0]).IsEqual(0);
        AssertThat(map.Labels[Size - 1]).IsEqual(-1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void FullyTransparentArtHasNoRegions()
    {
        var map = CardEffectRegionSegmenter.Segment(CardEffectArtPainter.Paint(Size, Size, (_, _) => Clear), Size, Size);

        AssertThat(map.RegionCount).IsEqual(0);
        AssertThat(map.Labels.All(label => label == -1)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ASpeckSmallerThanMergeBelowIsAbsorbedByItsNeighbour()
    {
        // A 2 x 2 blue speck is 4 pixels, under merge_below = 5.
        var map = CardEffectRegionSegmenter.Segment(
            CardEffectArtPainter.Paint(Size, Size, (x, y) => x is 10 or 11 && y is 10 or 11 ? Blue : Red), Size, Size);

        AssertThat(map.RegionCount).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ASpeckOfMergeBelowPixelsOrMoreSurvivesAsItsOwnRegion()
    {
        // A 3 x 2 blue patch is 6 pixels, not under merge_below = 5.
        var map = CardEffectRegionSegmenter.Segment(
            CardEffectArtPainter.Paint(Size, Size, (x, y) => x is >= 10 and <= 12 && y is 10 or 11 ? Blue : Red), Size, Size);

        AssertThat(map.RegionCount).IsEqual(2);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ALargeImageIsScaledDownSoItsLongestSideIs128Pixels()
    {
        var map = CardEffectRegionSegmenter.Segment(CardEffectArtPainter.Paint(256, 192, (_, _) => Red), 256, 192);

        AssertThat(map.Width).IsEqual(128);
        AssertThat(map.Height).IsEqual(96);
        AssertThat(map.Labels.Length).IsEqual(128 * 96);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ASmallImageIsScaledUpSoItsLongestSideIs128Pixels()
    {
        var map = CardEffectRegionSegmenter.Segment(CardEffectArtPainter.Paint(16, 32, (_, _) => Red), 16, 32);

        AssertThat(map.Width).IsEqual(64);
        AssertThat(map.Height).IsEqual(128);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void SegmentingTheSameImageTwiceGivesTheSameLabels()
    {
        var rgba = CardEffectArtPainter.Paint(Size, Size, (x, y) => (x + y) % 40 < 20 ? Red : Blue);

        var first = CardEffectRegionSegmenter.Segment(rgba, Size, Size);
        var second = CardEffectRegionSegmenter.Segment(rgba, Size, Size);

        AssertThat(first.Labels.SequenceEqual(second.Labels)).IsTrue();
        AssertThat(first.RegionCount).IsEqual(second.RegionCount);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void LabelsAreCompactSoEveryIdBelowRegionCountIsUsed()
    {
        var map = CardEffectRegionSegmenter.Segment(
            CardEffectArtPainter.Paint(Size, Size, (x, y) => x is >= 10 and <= 12 && y is 10 or 11 ? Blue : Red), Size, Size);

        var used = map.Labels.Where(label => label >= 0).Distinct().OrderBy(label => label).ToArray();

        AssertThat(used.SequenceEqual(Enumerable.Range(0, map.RegionCount))).IsTrue();
    }
}
