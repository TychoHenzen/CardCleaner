using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Card.Services;
using Godot;
using CardCleaner.Scripts.Features.Card.Models.EffectBaking;
using CardCleaner.Tests.Features.Card.Models.EffectBaking;

namespace CardCleaner.Tests.Features.Card.Services;

/// <summary>
///     The cache bakes each art texture's bevel map once, off the main thread, and hands it to everyone who asked;
///     a request never reads the art back or bakes it while the caller waits.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CardEffectNormalMapCacheTest
{
    private const string RealArt = "res://Assets/Graphics/CardSorted/CardArt/Barbarian_icons_01_t.PNG";

    [AfterTest]
    public static void TearDown()
    {
        CardEffectNormalMapCache.ResetForTesting();
    }

    private static ImageTexture TwoColourArt(int width, int height)
    {
        var rgba = CardEffectArtPainter.TwoHalves(width, height, Colors.Red, Colors.Blue);
        return ImageTexture.CreateFromImage(Image.CreateFromData(width, height, false, Image.Format.Rgba8, rgba));
    }

    [TestCase]
    [TestCategory("Unit")]
    public static async Task ARequestReturnsBeforeTheMapIsBakedAndHandsItOverLater()
    {
        ImageTexture? map = null;

        CardEffectNormalMapCache.Request(TwoColourArt(64, 64), baked => map = baked);

        AssertThat(map).OverrideFailureMessage("the map was baked while the caller waited").IsNull();
        await CardEffectBakeWait.Until(() => map != null, "the bevel map bake");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static async Task ManyRequestsForOneTextureShareOneBake()
    {
        var art = TwoColourArt(64, 64);
        ImageTexture? first = null;
        ImageTexture? second = null;

        CardEffectNormalMapCache.Request(art, baked => first = baked);
        CardEffectNormalMapCache.Request(art, baked => second = baked);
        await CardEffectBakeWait.Until(() => first != null && second != null, "both requests");
        ImageTexture? later = null;
        CardEffectNormalMapCache.Request(art, baked => later = baked);

        AssertThat(second).IsSame(first);
        AssertThat(later).OverrideFailureMessage("a baked map is handed over at once").IsSame(first);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static async Task DifferentTexturesGetTheirOwnNormalMaps()
    {
        var first = await CardEffectBakeWait.BakeOf(TwoColourArt(64, 64));
        var second = await CardEffectBakeWait.BakeOf(TwoColourArt(64, 64));

        AssertThat(ReferenceEquals(first, second)).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static async Task TheNormalMapIsAnRgba8ImageAtTheSegmentedSize()
    {
        var map = await CardEffectBakeWait.BakeOf(TwoColourArt(256, 192));

        AssertThat(map.GetWidth()).IsEqual(CardEffectRegionSegmenter.MaxDimension);
        AssertThat(map.GetHeight()).IsEqual(96);
        AssertThat(map.GetImage().GetFormat()).IsEqual(Image.Format.Rgba8);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static async Task TheTextureHoldsExactlyTheBakedBytes()
    {
        var image = TwoColourArt(128, 128).GetImage();
        var expected = CardEffectNormalBaker.Bake(image.GetData(), 128, 128);

        var actual = (await CardEffectBakeWait.BakeOf(ImageTexture.CreateFromImage(image))).GetImage().GetData();

        AssertThat(actual).IsEqual(expected.Rgba);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static async Task ARealCardArtTextureHasBeveledRegions()
    {
        var map = (await CardEffectBakeWait.BakeOf(GD.Load<Texture2D>(RealArt))).GetImage();

        var beveled = 0;
        for (var y = 0; y < map.GetHeight(); y++)
        for (var x = 0; x < map.GetWidth(); x++)
            if (map.GetPixel(x, y).B > 0f)
                beveled++;

        AssertThat(beveled).IsGreater(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static async Task AResetDropsTheBakesStillInFlight()
    {
        // A bake a test started must not hand its map to the next test.
        var dropped = false;
        CardEffectNormalMapCache.Request(TwoColourArt(64, 64), _ => dropped = true);

        CardEffectNormalMapCache.ResetForTesting();
        await CardEffectBakeWait.BakeOf(TwoColourArt(64, 64));
        await CardEffectBakeWait.Frames(2);

        AssertThat(dropped).IsFalse();
    }
}
