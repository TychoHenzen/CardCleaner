using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Features.Worldgen.Structures;
using CardCleaner.Scripts.Features.Worldgen.WeightModifiers;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Features.Worldgen.Structures;

[TestSuite]
[RequireGodotRuntime]
public class StructureStampTest
{
    [TestCase]
    public void TestEmptyStampReturnsNullForTiles()
    {
        var stamp = new StructureStamp { Id = "empty", Size = new Vector2I(2, 2) };

        AssertThat(stamp.GetTileAt(new Vector2I(0, 0))).IsNull();
        AssertThat(stamp.GetTileAt(new Vector2I(1, 1))).IsNull();
    }

    [TestCase]
    public void TestGetTileAtReturnsCorrectTile()
    {
        var stamp = new StructureStamp
        {
            Id = "test",
            Size = new Vector2I(2, 2),
            Tiles =
            [
                new StructureTileEntry(new Vector2I(0, 0), "corner_nw"),
                new StructureTileEntry(new Vector2I(1, 0), "corner_ne"),
                new StructureTileEntry(new Vector2I(0, 1), "corner_sw"),
                new StructureTileEntry(new Vector2I(1, 1), "corner_se")
            ]
        };

        AssertString(stamp.GetTileAt(new Vector2I(0, 0))).IsEqual("corner_nw");
        AssertString(stamp.GetTileAt(new Vector2I(1, 0))).IsEqual("corner_ne");
        AssertString(stamp.GetTileAt(new Vector2I(0, 1))).IsEqual("corner_sw");
        AssertString(stamp.GetTileAt(new Vector2I(1, 1))).IsEqual("corner_se");
    }

    [TestCase]
    public void TestGetTileAtOutOfBoundsReturnsNull()
    {
        var stamp = new StructureStamp
        {
            Id = "test",
            Size = new Vector2I(1, 1),
            Tiles = [new StructureTileEntry(new Vector2I(0, 0), "tile")]
        };

        AssertThat(stamp.GetTileAt(new Vector2I(5, 5))).IsNull();
        AssertThat(stamp.GetTileAt(new Vector2I(-1, 0))).IsNull();
    }

    [TestCase]
    public void TestGetAllTilesReturnsAllEntries()
    {
        var stamp = new StructureStamp
        {
            Id = "test",
            Size = new Vector2I(2, 1),
            Tiles =
            [
                new StructureTileEntry(new Vector2I(0, 0), "left"),
                new StructureTileEntry(new Vector2I(1, 0), "right")
            ]
        };

        var allTiles = stamp.GetAllTiles();

        AssertThat(allTiles.Count).IsEqual(2);
        AssertBool(allTiles.ContainsKey(new Vector2I(0, 0))).IsTrue();
        AssertBool(allTiles.ContainsKey(new Vector2I(1, 0))).IsTrue();
    }

    [TestCase]
    public void TestIsBiomeAllowedEmptyListAllowsAll()
    {
        var stamp = new StructureStamp { Id = "test" };

        AssertBool(stamp.IsBiomeAllowed(BiomeType.Plains)).IsTrue();
        AssertBool(stamp.IsBiomeAllowed(BiomeType.Forest)).IsTrue();
        AssertBool(stamp.IsBiomeAllowed(BiomeType.Desert)).IsTrue();
    }

    [TestCase]
    public void TestIsBiomeAllowedRestrictsToList()
    {
        var stamp = new StructureStamp
        {
            Id = "test",
            AllowedBiomes = [BiomeType.Plains, BiomeType.Forest]
        };

        AssertBool(stamp.IsBiomeAllowed(BiomeType.Plains)).IsTrue();
        AssertBool(stamp.IsBiomeAllowed(BiomeType.Forest)).IsTrue();
        AssertBool(stamp.IsBiomeAllowed(BiomeType.Desert)).IsFalse();
        AssertBool(stamp.IsBiomeAllowed(BiomeType.Tundra)).IsFalse();
    }

    [TestCase]
    public void TestContainsOffsetChecksSize()
    {
        var stamp = new StructureStamp { Id = "test", Size = new Vector2I(3, 2) };

        AssertBool(stamp.ContainsOffset(new Vector2I(0, 0))).IsTrue();
        AssertBool(stamp.ContainsOffset(new Vector2I(2, 1))).IsTrue();
        AssertBool(stamp.ContainsOffset(new Vector2I(3, 0))).IsFalse();
        AssertBool(stamp.ContainsOffset(new Vector2I(0, 2))).IsFalse();
        AssertBool(stamp.ContainsOffset(new Vector2I(-1, 0))).IsFalse();
    }

    [TestCase]
    public void TestGetAffinityMapReturnsCorrectValues()
    {
        var stamp = new StructureStamp
        {
            Id = "test",
            TileAffinities =
            [
                new TileAffinityEntry("cobblestone", 1.8f),
                new TileAffinityEntry("grass", 0.5f)
            ]
        };

        var affinityMap = stamp.GetAffinityMap();

        AssertThat(affinityMap.Count).IsEqual(2);
        AssertThat(affinityMap["cobblestone"]).IsEqual(1.8f);
        AssertThat(affinityMap["grass"]).IsEqual(0.5f);
    }

    [TestCase]
    public void TestDefaultValues()
    {
        var stamp = new StructureStamp();

        AssertString(stamp.Id).IsEqual("");
        AssertThat(stamp.Size).IsEqual(new Vector2I(1, 1));
        AssertThat(stamp.SpawnWeight).IsEqual(1.0f);
        AssertThat(stamp.MinSpacing).IsEqual(5);
        AssertThat(stamp.InfluenceRadius).IsEqual(3);
    }

    [TestCase]
    public void TestCacheInvalidationOnMarkDirty()
    {
        var stamp = new StructureStamp
        {
            Id = "test",
            Size = new Vector2I(1, 1),
            Tiles = [new StructureTileEntry(new Vector2I(0, 0), "original")]
        };

        // Access to populate cache
        var tile1 = stamp.GetTileAt(new Vector2I(0, 0));
        AssertString(tile1).IsEqual("original");

        // Modify and mark dirty
        stamp.Tiles[0] = new StructureTileEntry(new Vector2I(0, 0), "modified");
        stamp.MarkDirty();

        // Should return new value
        var tile2 = stamp.GetTileAt(new Vector2I(0, 0));
        AssertString(tile2).IsEqual("modified");
    }
}
