using System.Linq;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Worldgen;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using Godot;

namespace CardCleaner.Tests.Features.Deckbuilder.Services.SimpleMapGeneratorScenarios;

/// <summary>
///     SimpleMapGenerator biome tile selection and coherence scenarios split out of SimpleMapGeneratorTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class SimpleMapGeneratorBiomeTest : SimpleMapGeneratorTestBase
{
    [TestCase]
    public void TestGenerateMapUsesBiomeTiles()
    {
        // Skip during TSX migration if tile registry lacks required tiles
        if (_tileRegistry.GetAllTiles().Count() < 10)
        {
            GD.Print("[Migration] Skipping biome tiles test: tile registry has <10 tiles");
            return;
        }

        var size = new Vector2I(10, 10);
        var generator = CreateGenerator(size);

        var mapData = generator.GenerateMap(size);

        // Verify all tiles are from registered biome tiles
        for (var y = 0; y < size.Y; y++)
        for (var x = 0; x < size.X; x++)
        {
            var tileId = mapData.TileIds[y, x];
            // Tile ID should not be null/empty
            AssertBool(!string.IsNullOrEmpty(tileId)).IsTrue();
            // Tile should be registered in the tile registry
            var tile = _tileRegistry.GetTile(tileId);
            AssertThat(tile).IsNotNull();
        }
    }

    [TestCase]
    public void TestDifferentBiomesProduceDifferentTileDistributions()
    {
        // Skip during TSX migration if tile registry lacks biome-specific tiles
        if (_tileRegistry.GetAllTiles().Count() < 10)
        {
            GD.Print("[Migration] Skipping biome distribution test: tile registry has <10 tiles");
            return;
        }

        var size = new Vector2I(20, 20);

        // Hot signature should produce desert tiles
        _rng.Seed = 42;
        var hotGradient = new ConstantBiomeGradient(new CardSignature(new[] { 0.3f, 0.8f, 0.3f, 0f, 0f, 0f, 0f, 0f }));
        var hotProvider = new BiomeMapGenerator(_registry, hotGradient, size);
        var hotGenerator = new SimpleMapGenerator(_rng, hotProvider, _tileRegistry, _tileRegistry);
        var hotMap = hotGenerator.GenerateMap(size);

        // Cold signature should produce tundra tiles
        _rng.Seed = 42;
        var coldGradient = new ConstantBiomeGradient(new CardSignature(new[] { 0f, -0.8f, 0.4f, 0f, 0f, 0f, 0f, 0f }));
        var coldProvider = new BiomeMapGenerator(_registry, coldGradient, size);
        var coldGenerator = new SimpleMapGenerator(_rng, coldProvider, _tileRegistry, _tileRegistry);
        var coldMap = coldGenerator.GenerateMap(size);

        // Desert should have desert tiles, tundra should have tundra tiles
        var hotDesertTiles = CountTilesWithPrefix(hotMap, "desert_");
        var coldTundraTiles = CountTilesWithPrefix(coldMap, "tundra_");

        // Hot biome should produce desert tiles, cold biome should produce tundra tiles
        AssertThat(hotDesertTiles).IsGreater(0);
        AssertThat(coldTundraTiles).IsGreater(0);
    }

    private static int CountTilesWithPrefix(SimpleMapData map, string prefix)
    {
        var count = 0;
        for (var y = 0; y < map.Size.Y; y++)
        for (var x = 0; x < map.Size.X; x++)
            if (map.TileIds[y, x].StartsWith(prefix))
                count++;
        return count;
    }

    private static int CountTile(SimpleMapData map, string tileId)
    {
        var count = 0;
        for (var y = 0; y < map.Size.Y; y++)
        for (var x = 0; x < map.Size.X; x++)
            if (map.TileIds[y, x] == tileId)
                count++;
        return count;
    }

    [TestCase]
    public void TestBiomeCoherence_TilesMatchAssignedBiomes()
    {
        // Skip during TSX migration if tile registry lacks biome-specific tiles
        if (_tileRegistry.GetAllTiles().Count() < 10)
        {
            GD.Print("[Migration] Skipping biome coherence test: tile registry has <10 tiles");
            return;
        }

        var size = new Vector2I(25, 25);
        _rng.Seed = 12345;

        // Use desert signature to get clear biome assignment
        var desertSignature = new CardSignature(new[] { 0.3f, 0.7f, 0.3f, 0.4f, 0f, -0.2f, -0.2f, 0.3f });
        var gradient = new CardBasedGradient(new[] { desertSignature }, _rng);
        var biomeProvider = new BiomeMapGenerator(_registry, gradient, size);
        var generator = new SimpleMapGenerator(_rng, biomeProvider, _tileRegistry, _tileRegistry);

        var mapData = generator.GenerateMap(size);

        // Validate that tiles match their assigned biome regions
        var orphanTiles = 0;
        var totalTiles = 0;

        for (var y = 0; y < size.Y; y++)
        for (var x = 0; x < size.X; x++)
        {
            var position = new Vector2I(x, y);
            var expectedBiome = biomeProvider.GetBiomeAt(position);
            var placedTileId = mapData.TileIds[y, x];
            totalTiles++;

            // Check if tile belongs to expected biome
            var tileInPassable = expectedBiome.PassableTiles.GetAllTileIds().Contains(placedTileId);
            var tileInBlocked = expectedBiome.BlockedTiles.GetAllTileIds().Contains(placedTileId);

            if (!tileInPassable && !tileInBlocked)
            {
                orphanTiles++;
            }
        }

        var coherencePercentage = (totalTiles - orphanTiles) * 100.0f / totalTiles;

        // After fix: BiomeAffinityConstraint (3.0x) should dominate continuity (2.0x)
        // Expect >90% coherence (allowing some edge case orphans at biome boundaries)
        AssertThat(coherencePercentage).IsGreaterEqual(90.0f);
        AssertThat(orphanTiles).IsLessEqual(totalTiles / 10); // Max 10% orphans
    }
}
