using System.Diagnostics;
using CardCleaner.Scripts.Features.Card.Services;
using Godot;
using CardCleaner.Scripts.Features.Card.Models.EffectBaking;
using CardCleaner.Tests.Features.Card.Models.EffectBaking;

namespace CardCleaner.Tests.Features.Card.Services;

[TestSuite]
[RequireGodotRuntime]
public class CardEffectNormalMapCacheTest
{
    private const string RealArt = "res://Assets/Graphics/CardSorted/CardArt/Barbarian_icons_01_t.PNG";

    private static ImageTexture TwoColourArt(int width, int height)
    {
        var rgba = CardEffectArtPainter.TwoHalves(width, height, Colors.Red, Colors.Blue);
        return ImageTexture.CreateFromImage(Image.CreateFromData(width, height, false, Image.Format.Rgba8, rgba));
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void BakingTheSameTextureTwiceReturnsTheSameNormalMap()
    {
        var art = TwoColourArt(64, 64);

        var first = CardEffectNormalMapCache.GetOrBake(art);
        var second = CardEffectNormalMapCache.GetOrBake(art);

        AssertThat(ReferenceEquals(first, second)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void DifferentTexturesGetTheirOwnNormalMaps()
    {
        var first = CardEffectNormalMapCache.GetOrBake(TwoColourArt(64, 64));
        var second = CardEffectNormalMapCache.GetOrBake(TwoColourArt(64, 64));

        AssertThat(ReferenceEquals(first, second)).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TheNormalMapIsAnRgba8ImageAtTheSegmentedSize()
    {
        var map = CardEffectNormalMapCache.GetOrBake(TwoColourArt(256, 192));

        AssertThat(map.GetWidth()).IsEqual(CardEffectRegionSegmenter.MaxDimension);
        AssertThat(map.GetHeight()).IsEqual(96);
        AssertThat(map.GetImage().GetFormat()).IsEqual(Image.Format.Rgba8);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TheTextureHoldsExactlyTheBakedBytes()
    {
        var image = TwoColourArt(128, 128).GetImage();
        var expected = CardEffectNormalBaker.Bake(image.GetData(), 128, 128);

        var actual = CardEffectNormalMapCache.GetOrBake(ImageTexture.CreateFromImage(image)).GetImage().GetData();

        AssertThat(actual).IsEqual(expected.Rgba);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ARealCardArtTextureBakesQuicklyAndHasBeveledRegions()
    {
        var art = GD.Load<Texture2D>(RealArt);
        var clock = Stopwatch.StartNew();

        var map = CardEffectNormalMapCache.GetOrBake(art).GetImage();

        clock.Stop();
        var beveled = 0;
        for (var y = 0; y < map.GetHeight(); y++)
        for (var x = 0; x < map.GetWidth(); x++)
            if (map.GetPixel(x, y).B > 0f)
                beveled++;

        AssertThat(beveled).IsGreater(0);
        AssertThat(clock.ElapsedMilliseconds).IsLess(2000L);
    }
}
