using System;
using System.Linq;
using CardCleaner.Scripts.Features.Card.Models;

namespace CardCleaner.Tests.Features.Card.Models;

[TestSuite]
[RequireGodotRuntime]
public class CardEffectNormalBakerTest
{
    private const int Size = CardEffectRegionSegmenter.MaxDimension;
    private const int Mid = Size / 2;
    private const byte Flat = 128;

    private static readonly (byte R, byte G, byte B, byte A) Red = (255, 0, 0, 255);
    private static readonly (byte R, byte G, byte B, byte A) Blue = (0, 0, 255, 255);
    private static readonly (byte R, byte G, byte B, byte A) Clear = (0, 0, 0, 0);

    private static byte[] Paint(System.Func<int, int, (byte R, byte G, byte B, byte A)> pixelAt)
    {
        return CardEffectArtPainter.Paint(Size, Size, pixelAt);
    }

    private static CardEffectNormalMap BakeTwoHalves()
    {
        return CardEffectNormalBaker.Bake(Paint((x, _) => x < Mid ? Red : Blue), Size, Size);
    }

    private static int At(int x, int y)
    {
        return (y * Size + x) * 4;
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TheNormalMapHasTheSegmentedSizeAndFourBytesPerPixel()
    {
        var map = CardEffectNormalBaker.Bake(Paint((_, _) => Red), Size, Size);

        AssertThat(map.Width).IsEqual(Size);
        AssertThat(map.Height).IsEqual(Size);
        AssertThat(map.Rgba.Length).IsEqual(Size * Size * 4);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void BakingTheSameArtTwiceIsByteForByteIdentical()
    {
        var art = Paint((x, y) => (x / 16 + y / 24) % 2 == 0 ? Red : Blue);

        var first = CardEffectNormalBaker.Bake(art, Size, Size);
        var second = CardEffectNormalBaker.Bake(art, Size, Size);

        AssertThat(first.Rgba.SequenceEqual(second.Rgba)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TheDeepInteriorOfARegionIsFlat()
    {
        var map = CardEffectNormalBaker.Bake(Paint((_, _) => Red), Size, Size);

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
        var map = CardEffectNormalBaker.Bake(Paint((_, _) => Red), Size, Size);

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
        var map = CardEffectNormalBaker.Bake(Paint((_, _) => Red), Size, Size);

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
        var map = CardEffectNormalBaker.Bake(Paint((_, _) => Red), Size, Size);

        var edge = map.Rgba[At(0, Mid) + 2];
        var inside = map.Rgba[At(2, Mid) + 2];

        AssertThat(edge).IsGreater(inside);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TransparentPixelsAreUncoveredAndCoveredPixelsAreMarkedInAlpha()
    {
        var map = CardEffectNormalBaker.Bake(Paint((x, _) => x < Mid ? Red : Clear), Size, Size);

        AssertThat(map.Rgba[At(10, 10) + 3]).IsEqual((byte)255);
        AssertThat(map.Rgba[At(Size - 10, 10) + 3]).IsEqual((byte)0);
        AssertThat(map.Rgba[At(Size - 10, 10) + 2]).IsEqual((byte)0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void StoredTiltStaysWithinTheMaximumBevelSlope()
    {
        var map = BakeTwoHalves();
        // A normal tilted by MaxTilt has a horizontal part of MaxTilt / sqrt(1 + MaxTilt^2); allow one byte of rounding.
        var limit = CardEffectNormalBaker.MaxTilt / MathF.Sqrt(1f + CardEffectNormalBaker.MaxTilt * CardEffectNormalBaker.MaxTilt)
                    + 2f / 255f;

        for (var i = 0; i < map.Rgba.Length; i += 4)
        {
            var x = (map.Rgba[i] / 255f - 0.5f) * 2f;
            var y = (map.Rgba[i + 1] / 255f - 0.5f) * 2f;
            AssertThat(MathF.Sqrt(x * x + y * y) <= limit).IsTrue();
        }
    }
}
