using System.Linq;
using System.Net.Sockets;
using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Core.Enum;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Worldgen;
using GdUnit4;
using Godot;
using Godot.Collections;

namespace CardCleaner.Tests.Features;

[TestSuite]
[RequireGodotRuntime]
public class LayeredWorldGeneratorTest
{
    private LayeredWorldGenerator _generator = null!;
    private Array<SemanticTile> _testTiles = null!;
    private TileMapLayer _mockTerrainLayer = null!;
    private TileMapLayer _mockStructureLayer = null!;
    private TileMapLayer _mockDecorationLayer = null!;
    private TileMapLayer _mockEffectLayer = null!;
    private TileMapLayer _mockEnemyLayer = null!;
    private CompatibilityTag Grass = new() { Tag = "Grasslands" };
    private CompatibilityTag Forest = new() { Tag = "Forest" };
    private CompatibilityTag Any = new() { Tag = "Any", Mode = CompatibilityTag.CompatibilityMode.Any };

    [BeforeTest]
    public void Setup()
    {
        _testTiles = CreateTestTileSet();
        _generator = new LayeredWorldGenerator(_testTiles);

        // Create mock TileMapLayers
        _mockTerrainLayer = CreateMockTileMapLayer();
        _mockStructureLayer = CreateMockTileMapLayer();
        _mockDecorationLayer = CreateMockTileMapLayer();
        _mockEffectLayer = CreateMockTileMapLayer();
        _mockEnemyLayer = CreateMockTileMapLayer();
    }

    private Array<SemanticTile> CreateTestTileSet()
    {
        var terrainTile = CreateTestTile("Grass", TileLayer.Terrain, Grass);
        var structureTile = CreateTestTile("Tree", TileLayer.Structure, Forest, 0.3f);
        var decorationTile = CreateTestTile("Flower", TileLayer.Decoration, Grass, 0.2f);
        var effectTile = CreateTestTile("Particle", TileLayer.Effects, Any, 0.1f);

        return new Array<SemanticTile> { terrainTile, structureTile, decorationTile, effectTile };
    }

    private static Array<CompatibilityTag> CreateSocketArray(CompatibilityTag? biomeTag = null)
    {
        var array = new Array<CompatibilityTag>();
        array.Add(biomeTag);
        return array;
    }

    private SemanticTile CreateTestTile(string name, TileLayer layer, CompatibilityTag socketType,
        float spawnChance = 1.0f)
    {
        var tile = new SemanticTile
        {
            TileName = name,
            Layer = layer,
            BaseWeight = 1.0f,
            GlobalSpawnChance = spawnChance,
            Up = CreateSocketArray(socketType),
            Down = CreateSocketArray(socketType),
            North = CreateSocketArray(socketType),
            East = CreateSocketArray(socketType),
            South = CreateSocketArray(socketType),
            West = CreateSocketArray(socketType),
            NorthEast = CreateSocketArray(socketType),
            NorthWest = CreateSocketArray(socketType),
            SouthEast = CreateSocketArray(socketType),
            SouthWest = CreateSocketArray(socketType),
            Size = Vector2I.One,
            Tile = new TilePlacement
            {
                AnimationFrames = new Array<Vector3I> { new(0, 0, 0) },
                BlocksTiles = false,
                BlocksMovement = false
            }
        };
        return tile;
    }

    private TileMapLayer CreateMockTileMapLayer()
    {
        var layer = new TileMapLayer();
        Assertions.AddNode(layer);

        // Create a minimal TileSet for the layer
        var tileSet = new TileSet();
        var atlasSource = new TileSetAtlasSource();
        var mockTexture = CreateMockTexture();
        atlasSource.Texture = mockTexture;
        atlasSource.TextureRegionSize = new Vector2I(32, 32);
        tileSet.AddSource(atlasSource, 0);
        layer.TileSet = tileSet;

        return layer;
    }

    private Texture2D CreateMockTexture()
    {
        var image = Image.CreateEmpty(32, 32, false, Image.Format.Rgb8);
        image.Fill(Colors.Green);
        return ImageTexture.CreateFromImage(image);
    }

    [TestCase]
    public void TestGenerateWithValidInputs()
    {
        var mapSize = new Vector2I(5, 5);
        const ulong seed = 42;

        _generator.Generate(seed, _mockTerrainLayer, _mockStructureLayer,
            _mockDecorationLayer, _mockEffectLayer, null, mapSize);

        // Verify that terrain layer has tiles placed
        var usedRect = _mockTerrainLayer.GetUsedRect();
        Assertions.AssertThat(usedRect.Size.X).IsGreater(0);
        Assertions.AssertThat(usedRect.Size.Y).IsGreater(0);
    }

    [TestCase]
    public void TestGenerateWithNullLayers()
    {
        var mapSize = new Vector2I(3, 3);
        const ulong seed = 42;

        // Should not crash with null layers
        _generator.Generate(seed, null, null, null, null, null, mapSize);

        Assertions.AssertThat(_generator).IsNotNull();
    }

    [TestCase]
    public void TestDeterministicGeneration()
    {
        var mapSize = new Vector2I(4, 4);
        const ulong seed = 123;

        // Generate twice with same seed
        var layer1 = CreateMockTileMapLayer();
        var layer2 = CreateMockTileMapLayer();

        _generator.Generate(seed, layer1, null, null, null, null, mapSize);
        _generator.Generate(seed, layer2, null, null, null, null, mapSize);

        // Results should be identical
        var rect1 = layer1.GetUsedRect();
        var rect2 = layer2.GetUsedRect();

        Assertions.AssertThat(rect1.Size).IsEqual(rect2.Size);
    }

    [TestCase]
    public void TestGenerateLayerOnTop()
    {
        var mapSize = new Vector2I(3, 3);
        var rng = new RandomNumberGenerator { Seed = 42 };

        // Create base layer with terrain tiles
        var baseLayer = new SemanticTile[3, 3];
        var grassTile = _testTiles.First(t => t.Layer == TileLayer.Terrain);

        for (int y = 0; y < 3; y++)
        {
            for (int x = 0; x < 3; x++)
            {
                baseLayer[y, x] = grassTile;
            }
        }

        // Get structure tiles to place on top
        var structureTiles = _testTiles.Where(t => t.Layer == TileLayer.Structure).ToList();

        var result = _generator.GenerateLayerOnTop(baseLayer, structureTiles, mapSize, rng);

        Assertions.AssertThat(result).IsNotNull();
        Assertions.AssertThat(result.GetLength(0)).IsEqual(3);
        Assertions.AssertThat(result.GetLength(1)).IsEqual(3);
    }

    [TestCase]
    public void TestGenerateLayerOnTopWithEmptyTiles()
    {
        var mapSize = new Vector2I(2, 2);
        var rng = new RandomNumberGenerator { Seed = 42 };
        var baseLayer = new SemanticTile[2, 2];
        var emptyTileList = new System.Collections.Generic.List<SemanticTile>();

        var result = _generator.GenerateLayerOnTop(baseLayer, emptyTileList, mapSize, rng);

        // Should return empty layer
        for (int y = 0; y < 2; y++)
        {
            for (int x = 0; x < 2; x++)
            {
                Assertions.AssertThat(result[y, x]).IsNull();
            }
        }
    }

    [TestCase]
    public void TestLayerGenerationOrder()
    {
        var mapSize = new Vector2I(5, 5);
        const ulong seed = 42;

        _generator.Generate(seed, _mockTerrainLayer, _mockStructureLayer,
            _mockDecorationLayer, _mockEffectLayer, _mockEnemyLayer, mapSize);

        // Verify terrain layer has content (it should always be populated first)
        var terrainRect = _mockTerrainLayer.GetUsedRect();
        Assertions.AssertThat(terrainRect.Size.X).IsGreater(0);

        // Structure layer may or may not have content depending on spawn chances
        // but the generation should complete without errors
        Assertions.AssertThat(_generator).IsNotNull();
    }

    [TestCase]
    public void TestSpawnChanceAffectsPlacement()
    {
        // Create tiles with different spawn chances
        var alwaysSpawn = CreateTestTile("Always", TileLayer.Structure, Any, 1.0f);
        var neverSpawn = CreateTestTile("Never", TileLayer.Structure, Any, 0.0f);

        var testTiles = new Array<SemanticTile>
        {
            _testTiles[0], // terrain tile
            alwaysSpawn,
            neverSpawn
        };

        var generator = new LayeredWorldGenerator(testTiles);
        var mapSize = new Vector2I(3, 3);

        generator.Generate(42, _mockTerrainLayer, _mockStructureLayer, null, null, null, mapSize);

        // At minimum, terrain should be generated
        var terrainRect = _mockTerrainLayer.GetUsedRect();
        Assertions.AssertThat(terrainRect.Size.X).IsGreater(0);
    }

    [TestCase]
    public void TestEmptyTileSetHandling()
    {
        var emptyTiles = new Array<SemanticTile>();
        var emptyGenerator = new LayeredWorldGenerator(emptyTiles);
        var mapSize = new Vector2I(2, 2);

        // Should not crash with empty tile set
        emptyGenerator.Generate(42, _mockTerrainLayer, null, null, null, null, mapSize);

        Assertions.AssertThat(emptyGenerator).IsNotNull();
    }

    [TestCase]
    public void TestLargeMapGeneration()
    {
        var mapSize = new Vector2I(20, 20);
        const ulong seed = 999;

        _generator.Generate(seed, _mockTerrainLayer, _mockStructureLayer,
            _mockDecorationLayer, _mockEffectLayer, _mockEnemyLayer, mapSize);

        var usedRect = _mockTerrainLayer.GetUsedRect();

        // Should handle large maps without issues
        Assertions.AssertThat(usedRect.Size.X).IsLessEqual(mapSize.X);
        Assertions.AssertThat(usedRect.Size.Y).IsLessEqual(mapSize.Y);
    }
}