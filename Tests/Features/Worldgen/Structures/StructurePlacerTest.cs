using System.Collections.Generic;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Structures;
using CardCleaner.Scripts.Features.Worldgen.WeightModifiers;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Features.Worldgen.Structures;

[TestSuite]
[RequireGodotRuntime]
public class StructurePlacerTest
{
    private RandomNumberGenerator _rng = null!;
    private BiomeDefinition _plainsBiome = null!;
    private BiomeDefinition _forestBiome = null!;

    [BeforeTest]
    public void Setup()
    {
        _rng = new RandomNumberGenerator();
        _rng.Seed = 12345;
        _plainsBiome = new BiomeDefinition("plains", new CardSignature(), new TilePool(), new TilePool());
        _forestBiome = new BiomeDefinition("forest", new CardSignature(), new TilePool(), new TilePool());
    }

    [TestCase]
    public void TestFindValidPositionsReturnsPositionsInBounds()
    {
        var placer = new StructurePlacer();
        var stamp = new StructureStamp { Id = "test", Size = new Vector2I(2, 2) };
        var mapSize = new Vector2I(10, 10);

        var positions = placer.FindValidPositions(
            stamp,
            mapSize,
            _ => _plainsBiome,
            _ => true, // All passable
            _rng);

        AssertThat(positions.Count).IsGreater(0);

        foreach (var pos in positions)
        {
            AssertThat(pos.X).IsGreaterEqual(0);
            AssertThat(pos.Y).IsGreaterEqual(0);
            AssertThat(pos.X + stamp.Size.X).IsLessEqual(mapSize.X);
            AssertThat(pos.Y + stamp.Size.Y).IsLessEqual(mapSize.Y);
        }
    }

    [TestCase]
    public void TestFindValidPositionsRespectsPassability()
    {
        var placer = new StructurePlacer();
        var stamp = new StructureStamp { Id = "test", Size = new Vector2I(2, 2) };
        var mapSize = new Vector2I(10, 10);
        var impassablePositions = new HashSet<Vector2I>
        {
            new(0, 0), new(1, 0), new(0, 1), new(1, 1) // Block top-left corner
        };

        var positions = placer.FindValidPositions(
            stamp,
            mapSize,
            _ => _plainsBiome,
            pos => !impassablePositions.Contains(pos),
            _rng);

        // Should not find (0,0) as a valid position
        AssertBool(positions.Contains(new Vector2I(0, 0))).IsFalse();
    }

    [TestCase]
    public void TestFindValidPositionsRespectsBiome()
    {
        var placer = new StructurePlacer();
        var stamp = new StructureStamp
        {
            Id = "test",
            Size = new Vector2I(1, 1),
            AllowedBiomes = ["forest"] // Only forest allowed
        };
        var mapSize = new Vector2I(10, 10);

        var positions = placer.FindValidPositions(
            stamp,
            mapSize,
            _ => _plainsBiome, // All positions are plains
            _ => true,
            _rng);

        // Should find no valid positions since all are plains but stamp requires forest
        AssertThat(positions.Count).IsEqual(0);
    }

    [TestCase]
    public void TestPlaceStampWritesToTileArray()
    {
        var placer = new StructurePlacer();
        var stamp = new StructureStamp
        {
            Id = "well",
            Size = new Vector2I(2, 2),
            Tiles =
            [
                new StructureTileEntry(new Vector2I(0, 0), "well_nw"),
                new StructureTileEntry(new Vector2I(1, 0), "well_ne"),
                new StructureTileEntry(new Vector2I(0, 1), "well_sw"),
                new StructureTileEntry(new Vector2I(1, 1), "well_se")
            ]
        };
        var mapSize = new Vector2I(10, 10);
        var tileIds = new string[mapSize.Y, mapSize.X];

        // Fill with grass
        for (var y = 0; y < mapSize.Y; y++)
        for (var x = 0; x < mapSize.X; x++)
            tileIds[y, x] = "grass";

        var result = placer.PlaceStamp(stamp, new Vector2I(5, 5), tileIds, mapSize);

        AssertBool(result).IsTrue();
        AssertString(tileIds[5, 5]).IsEqual("well_nw");
        AssertString(tileIds[5, 6]).IsEqual("well_ne");
        AssertString(tileIds[6, 5]).IsEqual("well_sw");
        AssertString(tileIds[6, 6]).IsEqual("well_se");
    }

    [TestCase]
    public void TestPlaceStampRegistersWithPlacer()
    {
        var placer = new StructurePlacer();
        var stamp = new StructureStamp
        {
            Id = "well",
            Size = new Vector2I(2, 2),
            Tiles = [new StructureTileEntry(new Vector2I(0, 0), "well")]
        };
        var mapSize = new Vector2I(10, 10);
        var tileIds = new string[mapSize.Y, mapSize.X];

        placer.PlaceStamp(stamp, new Vector2I(3, 3), tileIds, mapSize);

        AssertThat(placer.PlacedStructures.Count).IsEqual(1);
        AssertThat(placer.PlacedStructures[0].Position).IsEqual(new Vector2I(3, 3));
        AssertString(placer.PlacedStructures[0].StructureId).IsEqual("well");
    }

    [TestCase]
    public void TestPlaceStampOutOfBoundsReturnsFalse()
    {
        var placer = new StructurePlacer();
        var stamp = new StructureStamp { Id = "test", Size = new Vector2I(3, 3) };
        var mapSize = new Vector2I(10, 10);
        var tileIds = new string[mapSize.Y, mapSize.X];

        var result = placer.PlaceStamp(stamp, new Vector2I(9, 9), tileIds, mapSize);

        AssertBool(result).IsFalse();
    }

    [TestCase]
    public void TestSpacingEnforcedBetweenStructures()
    {
        var placer = new StructurePlacer();
        var stamp = new StructureStamp
        {
            Id = "test",
            Size = new Vector2I(1, 1),
            MinSpacing = 5,
            Tiles = [new StructureTileEntry(new Vector2I(0, 0), "structure")]
        };
        var mapSize = new Vector2I(20, 20);
        var tileIds = new string[mapSize.Y, mapSize.X];
        for (var y = 0; y < mapSize.Y; y++)
        for (var x = 0; x < mapSize.X; x++)
            tileIds[y, x] = "grass";

        // Place first structure
        placer.PlaceStamp(stamp, new Vector2I(5, 5), tileIds, mapSize);

        // Try to find valid positions - should not include positions too close
        var positions = placer.FindValidPositions(
            stamp,
            mapSize,
            _ => _plainsBiome,
            _ => true,
            _rng);

        // Positions within MinSpacing should not be valid
        foreach (var pos in positions)
        {
            var distance = pos.DistanceTo(new Vector2I(5, 5));
            AssertThat(distance).IsGreaterEqual(stamp.MinSpacing);
        }
    }

    [TestCase]
    public void TestClearRemovesAllPlacements()
    {
        var placer = new StructurePlacer();
        var stamp = new StructureStamp
        {
            Id = "test",
            Size = new Vector2I(1, 1),
            Tiles = [new StructureTileEntry(new Vector2I(0, 0), "tile")]
        };
        var mapSize = new Vector2I(10, 10);
        var tileIds = new string[mapSize.Y, mapSize.X];

        placer.PlaceStamp(stamp, new Vector2I(2, 2), tileIds, mapSize);
        placer.PlaceStamp(stamp, new Vector2I(8, 8), tileIds, mapSize);

        AssertThat(placer.PlacedStructures.Count).IsEqual(2);

        placer.Clear();

        AssertThat(placer.PlacedStructures.Count).IsEqual(0);
    }

    [TestCase]
    public void TestProximityModifierIntegration()
    {
        var proximityModifier = new StructureProximityModifier();
        var placer = new StructurePlacer(proximityModifier);
        var stamp = new StructureStamp
        {
            Id = "well",
            Size = new Vector2I(1, 1),
            InfluenceRadius = 3,
            Tiles = [new StructureTileEntry(new Vector2I(0, 0), "well")],
            TileAffinities = [new TileAffinityEntry("cobblestone", 1.8f)]
        };
        var mapSize = new Vector2I(10, 10);
        var tileIds = new string[mapSize.Y, mapSize.X];

        placer.PlaceStamp(stamp, new Vector2I(5, 5), tileIds, mapSize);

        // Proximity modifier should have the structure registered
        AssertThat(proximityModifier.PlacedStructures.Count).IsEqual(1);
    }

    [TestCase]
    public void TestTryPlaceRandomDeterministic()
    {
        var placer = new StructurePlacer();
        var stamp = new StructureStamp
        {
            Id = "test",
            Size = new Vector2I(2, 2),
            Tiles =
            [
                new StructureTileEntry(new Vector2I(0, 0), "a"),
                new StructureTileEntry(new Vector2I(1, 0), "b")
            ]
        };
        var mapSize = new Vector2I(10, 10);

        // Run twice with same seed
        _rng.Seed = 42;
        var tileIds1 = new string[mapSize.Y, mapSize.X];
        for (var y = 0; y < mapSize.Y; y++)
        for (var x = 0; x < mapSize.X; x++)
            tileIds1[y, x] = "grass";
        placer.TryPlaceRandom(stamp, mapSize, _ => _plainsBiome, _ => true, tileIds1, _rng);
        var pos1 = placer.PlacedStructures[0].Position;

        placer.Clear();

        _rng.Seed = 42;
        var tileIds2 = new string[mapSize.Y, mapSize.X];
        for (var y = 0; y < mapSize.Y; y++)
        for (var x = 0; x < mapSize.X; x++)
            tileIds2[y, x] = "grass";
        placer.TryPlaceRandom(stamp, mapSize, _ => _plainsBiome, _ => true, tileIds2, _rng);
        var pos2 = placer.PlacedStructures[0].Position;

        AssertThat(pos1).IsEqual(pos2);
    }
}
