using System;
using System.Linq;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Tests.Features.Card.Models;

[TestSuite]
[RequireGodotRuntime]
public class CardEffectNormalBakerTest
{
    private const int Size = CardEffectRegionSegmenter.MaxDimension;
    private const int Mid = Size / 2;
    private const byte Flat = 128;

    private static CardEffectNormalMap BakeOneColour()
    {
        return CardEffectNormalBaker.Bake(CardEffectArtPainter.Fill(Size, Size, Colors.Red), Size, Size);
    }

    private static CardEffectNormalMap BakeTwoHalves()
    {
        var art = CardEffectArtPainter.TwoHalves(Size, Size, Colors.Red, Colors.Blue);
        return CardEffectNormalBaker.Bake(art, Size, Size);
    }

    private static int At(int x, int y)
    {
        return (y * Size + x) * 4;
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TheNormalMapHasTheSegmentedSizeAndFourBytesPerPixel()
    {
        var map = BakeOneColour();

        AssertThat(map.Width).IsEqual(Size);
        AssertThat(map.Height).IsEqual(Size);
        AssertThat(map.Rgba.Length).IsEqual(Size * Size * 4);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void BakingTheSameArtTwiceIsByteForByteIdentical()
    {
        var art = CardEffectArtPainter.Paint(
            Size, Size, (x, y) => (x / 16 + y / 24) % 2 == 0 ? Colors.Red : Colors.Blue);

        var first = CardEffectNormalBaker.Bake(art, Size, Size);
        var second = CardEffectNormalBaker.Bake(art, Size, Size);

        AssertThat(first.Rgba.SequenceEqual(second.Rgba)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TheDeepInteriorOfARegionIsFlat()
    {
        var map = BakeOneColour();

        var centre = At(Mid, Mid);
        AssertThat(map.Rgba[centre]).IsEqual(Flat);
        AssertThat(map.Rgba[centre + 1]).IsEqual(Flat);
        AssertThat(map.Rgba[centre + 2]).IsEqual((byte)0);
        AssertThat(map.Rgba[centre + 3]).IsEqual((byte)255);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TheEdgeOfARegionTiltsOutwardAndCarriesBevelStrength()
    {
        var map = BakeOneColour();

        var left = At(0, Mid);
        var right = At(Size - 1, Mid);
        var top = At(Mid, 0);
        var bottom = At(Mid, Size - 1);

        AssertThat(map.Rgba[left]).IsLess(Flat);
        AssertThat(map.Rgba[right]).IsGreater(Flat);
        AssertThat(map.Rgba[top + 1]).IsLess(Flat);
        AssertThat(map.Rgba[bottom + 1]).IsGreater(Flat);
        AssertThat(map.Rgba[left + 2]).IsGreater((byte)0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TwoColourRegionsEachGetTheirOwnBevelAtTheSeam()
    {
        var map = BakeTwoHalves();

        var leftOfSeam = At(Mid - 1, Mid);
        var rightOfSeam = At(Mid, Mid);

        // The left region tilts towards the seam (+x), the right one back towards it (-x).
        AssertThat(map.Rgba[leftOfSeam]).IsGreater(Flat);
        AssertThat(map.Rgba[rightOfSeam]).IsLess(Flat);
        AssertThat(map.Rgba[leftOfSeam + 2]).IsGreater((byte)0);
        AssertThat(map.Rgba[rightOfSeam + 2]).IsGreater((byte)0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TheSameShapeInOneColourHasNoBevelWhereTheSeamWouldBe()
    {
        var map = BakeOneColour();

        var seam = At(Mid - 1, Mid);

        AssertThat(map.Rgba[seam]).IsEqual(Flat);
        AssertThat(map.Rgba[seam + 2]).IsEqual((byte)0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void EachRegionIsFlatInsideItsOwnBevel()
    {
        var map = BakeTwoHalves();

        AssertThat(map.Rgba[At(Mid / 2, Mid) + 2]).IsEqual((byte)0);
        AssertThat(map.Rgba[At(Mid + Mid / 2, Mid) + 2]).IsEqual((byte)0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void BevelStrengthFallsOffWithDistanceFromTheRegionEdge()
    {
        var map = BakeOneColour();

        var edge = map.Rgba[At(0, Mid) + 2];
        var inside = map.Rgba[At(2, Mid) + 2];

        AssertThat(edge).IsGreater(inside);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TransparentPixelsAreUncoveredAndCoveredPixelsAreMarkedInAlpha()
    {
        var art = CardEffectArtPainter.TwoHalves(Size, Size, Colors.Red, Colors.Transparent);

        var map = CardEffectNormalBaker.Bake(art, Size, Size);

        AssertThat(map.Rgba[At(10, 10) + 3]).IsEqual((byte)255);
        AssertThat(map.Rgba[At(Size - 10, 10) + 3]).IsEqual((byte)0);
        AssertThat(map.Rgba[At(Size - 10, 10) + 2]).IsEqual((byte)0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void StoredTiltStaysWithinTheMaximumBevelSlope()
    {
        var map = BakeTwoHalves();
        // A normal tilted by MaxTilt has a horizontal part of MaxTilt / sqrt(1 + MaxTilt^2); allow two bytes of
        // rounding.
        var tilt = CardEffectNormalBaker.MaxTilt;
        var limit = tilt / MathF.Sqrt(1f + tilt * tilt) + 2f / 255f;

        for (var i = 0; i < map.Rgba.Length; i += 4)
        {
            var x = (map.Rgba[i] / 255f - 0.5f) * 2f;
            var y = (map.Rgba[i + 1] / 255f - 0.5f) * 2f;
            AssertThat(MathF.Sqrt(x * x + y * y) <= limit).IsTrue();
        }
    }
}
