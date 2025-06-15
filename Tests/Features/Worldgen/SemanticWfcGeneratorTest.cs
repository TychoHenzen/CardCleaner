using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Features.Worldgen;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen;

[TestSuite]
[RequireGodotRuntime]
public class SemanticWfc3dGeneratorTest
{
    private SemanticTile[] _testTileSet = null!;
    private SemanticTile _grassTile = null!;
    private SemanticTile _stoneTile = null!;
    private SemanticTile _waterTile = null!;
    private static readonly CompatibilityTag Grasslands = new() { Tag = "Grasslands" };
    private static readonly CompatibilityTag Mountains = new() { Tag = "Mountains" };
    private static readonly CompatibilityTag Swamp = new() { Tag = "Swamp" };
    private static readonly CompatibilityTag Any = new() { Tag = "Any", Mode = CompatibilityTag.CompatibilityMode.Any };

    [BeforeTest]
    public void Setup()
    {
        _grassTile = CreateTestTile("Grass", Grasslands, Grasslands, Grasslands,
            Grasslands, TileLayer.Terrain);
        _stoneTile = CreateTestTile("Stone", Mountains, Mountains, Mountains,
            Mountains, TileLayer.Terrain);
        _waterTile = CreateTestTile("Water", Swamp, Swamp, Swamp, Swamp, TileLayer.Terrain);

        _testTileSet = new[] { _grassTile, _stoneTile, _waterTile };
    }

    private static Godot.Collections.Array<CompatibilityTag> CreateSocketArray(CompatibilityTag tag)
    {
        var array = new Godot.Collections.Array<CompatibilityTag> { tag };
        return array;
    }

    private static SemanticTile CreateTestTile(string name, CompatibilityTag north, CompatibilityTag east,
        CompatibilityTag south, CompatibilityTag west, TileLayer layer = TileLayer.Terrain, float weight = 1.0f)
    {
        var tile = new SemanticTile
        {
            TileName = name,
            SocketData = new SocketData()
            {
                North = CreateSocketArray(north),
                East = CreateSocketArray(east),
                South = CreateSocketArray(south),
                West = CreateSocketArray(west),
                NorthEast = CreateSocketArray(Any),
                NorthWest = CreateSocketArray(Any),
                SouthEast = CreateSocketArray(Any),
                SouthWest = CreateSocketArray(Any),
                Up = CreateSocketArray(Any),
                Down = CreateSocketArray(Any)
            },
            BaseWeight = weight,
            Passability = TilePassability.Passable,
            Layer = layer
        };
        return tile;
    }

    private static RandomNumberGenerator CreateRng(uint seed)
    {
        var rng = new RandomNumberGenerator();
        rng.Seed = seed;
        return rng;
    }

    [TestCase]
    public void TestGeneratorInitialization()
    {
        var mapSize = new Vector3I(10, 10, 1); // Single layer for terrain
        var rng = CreateRng(42);
        var generator = new SemanticWfc3dGenerator(_testTileSet, mapSize, rng);

        Assertions.AssertThat(generator).IsNotNull();
    }

    [TestCase]
    public void TestDeterministicGeneration()
    {
        var mapSize = new Vector3I(5, 5, 1); // Single layer
        const uint seed = 42;

        var generator1 = new SemanticWfc3dGenerator(_testTileSet, mapSize, CreateRng(seed));
        var result1 = generator1.Generate();

        var generator2 = new SemanticWfc3dGenerator(_testTileSet, mapSize, CreateRng(seed));
        var result2 = generator2.Generate();

        // Results should be identical with same seed (check terrain layer only)
        for (int y = 0; y < mapSize.Y; y++)
        {
            for (int x = 0; x < mapSize.X; x++)
            {
                Assertions.AssertThat(result1[0, y, x]?.TileName).IsEqual(result2[0, y, x]?.TileName);
            }
        }
    }

    [TestCase]
    public void TestDifferentSeedsProduceDifferentResults()
    {
        var mapSize = new Vector3I(5, 5, 1);

        var generator1 = new SemanticWfc3dGenerator(_testTileSet, mapSize, CreateRng(42));
        var result1 = generator1.Generate();

        var generator2 = new SemanticWfc3dGenerator(_testTileSet, mapSize, CreateRng(123));
        var result2 = generator2.Generate();

        // At least one tile should be different
        bool foundDifference = false;
        for (int y = 0; y < mapSize.Y && !foundDifference; y++)
        {
            for (int x = 0; x < mapSize.X && !foundDifference; x++)
            {
                if (result1[0, y, x]?.TileName != result2[0, y, x]?.TileName)
                {
                    foundDifference = true;
                }
            }
        }

        Assertions.AssertBool(foundDifference).IsTrue();
    }

    [TestCase]
    public void TestSmallMapGeneration()
    {
        var mapSize = new Vector3I(1, 1, 1);
        var generator = new SemanticWfc3dGenerator(_testTileSet, mapSize, CreateRng(42));

        var result = generator.Generate();

        Assertions.AssertThat(result).IsNotNull();
        Assertions.AssertThat(result[0, 0, 0]).IsNotNull();
        Assertions.AssertThat(_testTileSet).Contains(result[0, 0, 0]);
    }

    [TestCase]
    public void TestEmptyTileSetHandling()
    {
        var emptyTileSet = new SemanticTile[0];
        var mapSize = new Vector3I(3, 3, 1);
        var generator = new SemanticWfc3dGenerator(emptyTileSet, mapSize, CreateRng(42));

        var result = generator.Generate();

        // Should not crash, but all tiles will be null
        for (int y = 0; y < mapSize.Y; y++)
        {
            for (int x = 0; x < mapSize.X; x++)
            {
                Assertions.AssertThat(result[0, y, x]).IsNull();
            }
        }
    }

    [TestCase]
    public void TestSingleTileTypeGeneration()
    {
        var singleTileSet = new[] { _grassTile };
        var mapSize = new Vector3I(3, 3, 1);
        var generator = new SemanticWfc3dGenerator(singleTileSet, mapSize, CreateRng(42));

        var result = generator.Generate();

        // All tiles should be grass
        for (int y = 0; y < mapSize.Y; y++)
        {
            for (int x = 0; x < mapSize.X; x++)
            {
                Assertions.AssertThat(result[0, y, x]?.TileName).IsEqual("Grass");
            }
        }
    }

    [TestCase]
    public void TestWeightedTileSelection()
    {
        // Create tiles with very different weights
        var heavyTile = CreateTestTile("Heavy", Any, Any, Any, Any, TileLayer.Terrain, 100.0f);
        var lightTile = CreateTestTile("Light", Any, Any, Any, Any, TileLayer.Terrain, 0.1f);

        var weightedTileSet = new[] { heavyTile, lightTile };
        var mapSize = new Vector3I(20, 20, 1);
        var generator = new SemanticWfc3dGenerator(weightedTileSet, mapSize, CreateRng(42));

        var result = generator.Generate();

        // Count occurrences
        int heavyCount = 0;
        int lightCount = 0;

        for (int y = 0; y < mapSize.Y; y++)
        {
            for (int x = 0; x < mapSize.X; x++)
            {
                if (result[0, y, x]?.TileName == "Heavy") heavyCount++;
                else if (result[0, y, x]?.TileName == "Light") lightCount++;
            }
        }

        // Heavy tile should appear much more frequently than light tile
        Assertions.AssertThat(heavyCount).IsGreater(lightCount);
    }

    [TestCase]
    public void TestMultiLayerGeneration()
    {
        // Create tiles for different layers
        var terrainTile = CreateTestTile("TerrainTile", Any, Any, Any, Any, TileLayer.Terrain);
        var decorationTile = CreateTestTile("DecorationTile", Any, Any, Any, Any, TileLayer.Decoration);
        var structureTile = CreateTestTile("StructureTile", Any, Any, Any, Any, TileLayer.Structure);

        var multiLayerTileSet = new[] { terrainTile, decorationTile, structureTile };
        var mapSize = new Vector3I(3, 3, 3); // 3 layers

        var generator = new SemanticWfc3dGenerator(multiLayerTileSet, mapSize, CreateRng(42));
        var result = generator.Generate();

        // Verify each layer contains appropriate tiles
        for (int y = 0; y < mapSize.Y; y++)
        {
            for (int x = 0; x < mapSize.X; x++)
            {
                // Layer 0 (Terrain) should have terrain tiles
                Assertions.AssertThat(result[0, y, x]?.TileName).IsEqual("TerrainTile");
                // Layer 1 (Decoration) should have decoration tiles
                Assertions.AssertThat(result[1, y, x]?.TileName).IsEqual("DecorationTile");
                // Layer 2 (Structure) should have structure tiles
                Assertions.AssertThat(result[2, y, x]?.TileName).IsEqual("StructureTile");
            }
        }
    }

    [TestCase]
    public void TestGradientInfluenceIntegration()
    {
        // Test that generator works with gradient influence component
        var mapSize = new Vector3I(5, 5, 1);
        var rng = CreateRng(42);

        // Create a null gradient component (should work without gradient)
        var generator = new SemanticWfc3dGenerator(_testTileSet, mapSize, rng, null);
        var result = generator.Generate();

        Assertions.AssertThat(result).IsNotNull();

        // Verify all positions have valid tiles
        for (int y = 0; y < mapSize.Y; y++)
        {
            for (int x = 0; x < mapSize.X; x++)
            {
                Assertions.AssertThat(result[0, y, x]).IsNotNull();
            }
        }
    }
}