using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features;

[TestSuite]
public class TilePatternTest
{
    [TestCase]
    public void TestTilePatternCreation()
    {
        var pattern = new TilePattern
        {
            PatternName = "TestPattern",
            Size = new Vector2I(2, 2),
            Tiles = new[]
            {
                new TilePlacement { AtlasCoords = new Vector2I(0, 0) },
                new TilePlacement { AtlasCoords = new Vector2I(1, 0) },
                new TilePlacement { AtlasCoords = new Vector2I(0, 1) },
                new TilePlacement { AtlasCoords = new Vector2I(1, 1) }
            }
        };

        Assertions.AssertThat(pattern.PatternName).IsEqual("TestPattern");
        Assertions.AssertThat(pattern.Size).IsEqual(new Vector2I(2, 2));
        Assertions.AssertThat(pattern.Tiles.Length).IsEqual(4);
    }

    [TestCase]
    public void TestGetTileAt()
    {
        var pattern = new TilePattern
        {
            Size = new Vector2I(2, 2),
            Tiles = new[]
            {
                new TilePlacement { AtlasCoords = new Vector2I(0, 0) }, // [0,0]
                new TilePlacement { AtlasCoords = new Vector2I(1, 0) }, // [1,0]
                new TilePlacement { AtlasCoords = new Vector2I(0, 1) }, // [0,1]
                new TilePlacement { AtlasCoords = new Vector2I(1, 1) }  // [1,1]
            }
        };

        var tile00 = pattern.GetTileAt(new Vector2I(0, 0));
        var tile10 = pattern.GetTileAt(new Vector2I(1, 0));
        var tile01 = pattern.GetTileAt(new Vector2I(0, 1));
        var tile11 = pattern.GetTileAt(new Vector2I(1, 1));

        Assertions.AssertThat(tile00?.AtlasCoords).IsEqual(new Vector2I(0, 0));
        Assertions.AssertThat(tile10?.AtlasCoords).IsEqual(new Vector2I(1, 0));
        Assertions.AssertThat(tile01?.AtlasCoords).IsEqual(new Vector2I(0, 1));
        Assertions.AssertThat(tile11?.AtlasCoords).IsEqual(new Vector2I(1, 1));
    }

    [TestCase]
    public void TestGetTileAtOutOfBounds()
    {
        var pattern = new TilePattern
        {
            Size = new Vector2I(2, 2),
            Tiles = new[]
            {
                new TilePlacement { AtlasCoords = new Vector2I(0, 0) },
                new TilePlacement { AtlasCoords = new Vector2I(1, 0) },
                new TilePlacement { AtlasCoords = new Vector2I(0, 1) },
                new TilePlacement { AtlasCoords = new Vector2I(1, 1) }
            }
        };

        var outOfBounds = pattern.GetTileAt(new Vector2I(2, 2));
        var negative = pattern.GetTileAt(new Vector2I(-1, -1));

        Assertions.AssertThat(outOfBounds).IsNull();
        Assertions.AssertThat(negative).IsNull();
    }

    [TestCase]
    public void TestEmptyPattern()
    {
        var emptyPattern = new TilePattern
        {
            Size = Vector2I.Zero,
            Tiles = System.Array.Empty<TilePlacement>()
        };

        var tile = emptyPattern.GetTileAt(Vector2I.Zero);
        Assertions.AssertThat(tile).IsNull();
    }
}