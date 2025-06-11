using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Core.Enum;
using CardCleaner.Scripts.Features.Worldgen;
using GdUnit4;
using Godot;
using Godot.Collections;

namespace CardCleaner.Tests.Features.Worldgen;

[TestSuite]
[RequireGodotRuntime]
public class SemanticWfcGeneratorTest
{
    private Array<SemanticTile> _testTileSet = null!;
    private SemanticTile _grassTile = null!;
    private SemanticTile _stoneTile = null!;
    private SemanticTile _waterTile = null!;
    private static readonly CompatibilityTag Grasslands = new(){Tag = "Grasslands"};
    private static readonly CompatibilityTag Mountains = new(){Tag = "Mountains"};
    private static readonly CompatibilityTag Swamp = new(){Tag = "Swamp"};
    private static readonly CompatibilityTag Any = new(){Tag = "Any", Mode = CompatibilityTag.CompatibilityMode.Any};

    [BeforeTest]
    public void Setup()
    {
        _grassTile = CreateTestTile("Grass", Grasslands, Grasslands, Grasslands,
            Grasslands);
        _stoneTile = CreateTestTile("Stone", Mountains, Mountains, Mountains,
            Mountains);
        _waterTile = CreateTestTile("Water", Swamp, Swamp, Swamp, Swamp);

        _testTileSet = new Array<SemanticTile> { _grassTile, _stoneTile, _waterTile };
    }

    private static Array<CompatibilityTag> CreateSocketArray(CompatibilityTag tag)
    {
        var array = new Array<CompatibilityTag> { tag };
        return array;
    }

    private static SemanticTile CreateTestTile(string name, CompatibilityTag north, CompatibilityTag east,
        CompatibilityTag south, CompatibilityTag west, float weight = 1.0f)
    {
        var tile = new SemanticTile
        {
            TileName = name,
            North = CreateSocketArray(north),
            East = CreateSocketArray(east),
            South = CreateSocketArray(south),
            West = CreateSocketArray(west),
            NorthEast = CreateSocketArray(Any),
            NorthWest = CreateSocketArray(Any),
            SouthEast = CreateSocketArray(Any),
            SouthWest = CreateSocketArray(Any),
            Up = CreateSocketArray(Any),
            Down = CreateSocketArray(Any),
            BaseWeight = weight,
            Passability = TilePassability.Passable,
            Layer = TileLayer.Terrain
        };
        return tile;
    }


    [TestCase]
    public void TestGeneratorInitialization()
    {
        var mapSize = new Vector2I(10, 10);
        var generator = new SemanticWfcGenerator(_testTileSet, mapSize, 42);

        Assertions.AssertThat(generator).IsNotNull();
    }

    [TestCase]
    public void TestDeterministicGeneration()
    {
        var mapSize = new Vector2I(5, 5);
        const uint seed = 42;

        var generator1 = new SemanticWfcGenerator(_testTileSet, mapSize, seed);
        var result1 = generator1.Generate();

        var generator2 = new SemanticWfcGenerator(_testTileSet, mapSize, seed);
        var result2 = generator2.Generate();

        // Results should be identical with same seed
        for (int y = 0; y < mapSize.Y; y++)
        {
            for (int x = 0; x < mapSize.X; x++)
            {
                Assertions.AssertThat(result1[y, x]?.TileName).IsEqual(result2[y, x].TileName);
            }
        }
    }

    [TestCase]
    public void TestDifferentSeedsProduceDifferentResults()
    {
        var mapSize = new Vector2I(5, 5);

        var generator1 = new SemanticWfcGenerator(_testTileSet, mapSize, 42);
        var result1 = generator1.Generate();

        var generator2 = new SemanticWfcGenerator(_testTileSet, mapSize, 123);
        var result2 = generator2.Generate();

        // At least one tile should be different
        bool foundDifference = false;
        for (int y = 0; y < mapSize.Y && !foundDifference; y++)
        {
            for (int x = 0; x < mapSize.X && !foundDifference; x++)
            {
                if (result1[y, x]?.TileName != result2[y, x]?.TileName)
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
        var mapSize = new Vector2I(1, 1);
        var generator = new SemanticWfcGenerator(_testTileSet, mapSize, 42);

        var result = generator.Generate();

        Assertions.AssertThat(result).IsNotNull();
        Assertions.AssertThat(result[0, 0]).IsNotNull();
        Assertions.AssertThat(_testTileSet.Contains(result[0, 0])).IsTrue();
    }

    [TestCase]
    public void TestEmptyTileSetHandling()
    {
        var emptyTileSet = new Array<SemanticTile>();
        var mapSize = new Vector2I(3, 3);
        var generator = new SemanticWfcGenerator(emptyTileSet, mapSize, 42);

        var result = generator.Generate();

        // Should not crash, but all tiles will be null
        for (int y = 0; y < mapSize.Y; y++)
        {
            for (int x = 0; x < mapSize.X; x++)
            {
                Assertions.AssertThat(result[y, x]).IsNull();
            }
        }
    }

    [TestCase]
    public void TestSingleTileTypeGeneration()
    {
        var singleTileSet = new Array<SemanticTile> { _grassTile };
        var mapSize = new Vector2I(3, 3);
        var generator = new SemanticWfcGenerator(singleTileSet, mapSize, 42);

        var result = generator.Generate();

        // All tiles should be grass
        for (int y = 0; y < mapSize.Y; y++)
        {
            for (int x = 0; x < mapSize.X; x++)
            {
                Assertions.AssertThat(result[y, x]?.TileName).IsEqual("Grass");
            }
        }
    }

    [TestCase]
    public void TestWeightedTileSelection()
    {
        // Create tiles with very different weights, 
        var heavyTile = CreateTestTile("Heavy", Any, Any, Any, Any, 100.0f);
        var lightTile = CreateTestTile("Light", Any, Any, Any, Any, 0.1f);

        var weightedTileSet = new Array<SemanticTile> { heavyTile, lightTile };
        var mapSize = new Vector2I(20, 20);
        var generator = new SemanticWfcGenerator(weightedTileSet, mapSize, 42);

        var result = generator.Generate();

        // Count occurrences
        int heavyCount = 0;
        int lightCount = 0;

        for (int y = 0; y < mapSize.Y; y++)
        {
            for (int x = 0; x < mapSize.X; x++)
            {
                if (result[y, x]?.TileName == "Heavy") heavyCount++;
                else if (result[y, x]?.TileName == "Light") lightCount++;
            }
        }

        // Heavy tile should appear much more frequently than light tile
        Assertions.AssertThat(heavyCount).IsGreater(lightCount);
    }
}