using CardCleaner.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling;

[TestSuite]
[RequireGodotRuntime]
public class TileAutoTileFormatsTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void UnknownFormatResolvesToNothing()
    {
        var tile = TileOf("nonexistent_format_xyz");

        AssertThat(tile.GetAutoTileFormat()).IsNull();
        AssertThat(tile.GetVariantDefinition(0)).IsNull();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void UnknownFormatExpectsSixteenVariants()
    {
        AssertInt(TileOf("nonexistent_format_xyz").GetExpectedVariantCount()).IsEqual(16);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void KnownFormatMatchesTheRegistry()
    {
        var tile = TileOf("blob47");

        AssertBool(AutoTileFormatRegistry.TryGet("blob47", out var expected)).IsTrue();
        AssertThat(tile.GetAutoTileFormat()).IsSame(expected);
        AssertInt(tile.GetExpectedVariantCount()).IsEqual(47);
    }

    private static TileDefinition TileOf(string formatName) => new(
        "t",
        "T",
        TilePassability.Passable,
        new Vector2I(0, 0),
        new TileDefinitionOptions { AutoTileFormatName = formatName });
}
