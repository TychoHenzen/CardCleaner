using System.Collections.Generic;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.Wfc.BiomeGridScenarios;

/// <summary>
///     Shared fixture for the BiomeGridIntegrationTest scenario suites.
/// </summary>
public abstract class BiomeGridIntegrationTestBase
{
    protected WfcAdjacencyRules _rules = null!;

    protected BiomeRegistry _registry = null!;

    protected TileRegistry _tileRegistry = null!;

    [BeforeTest]
    public void Setup()
    {
        // Create simple adjacency rules for test tiles
        _rules = new WfcAdjacencyRules(new[]
        {
            ("fire_tile", "fire_tile"),
            ("fire_tile", "water_tile"),
            ("water_tile", "water_tile"),
            ("water_tile", "neutral_tile"),
            ("neutral_tile", "neutral_tile"),
            ("neutral_tile", "fire_tile")
        });

        // Create a tile registry with test tiles
        // Tiles need AllowedBiomes set for BiomeAffinityConstraint to work
        _tileRegistry = new TileRegistry();
        _tileRegistry.Clear(); // Clear production tiles loaded by constructor
        _tileRegistry.RegisterTile(new TileDefinition(
            "fire_tile",
            "Fire Tile",
            TilePassability.Passable,
            Vector2I.Zero,
            new TileDefinitionOptions
            {
                AllowedBiomes = new HashSet<string> { "fire" }
            }));
        _tileRegistry.RegisterTile(new TileDefinition(
            "water_tile",
            "Water Tile",
            TilePassability.Passable,
            Vector2I.Zero,
            new TileDefinitionOptions
            {
                AllowedBiomes = new HashSet<string> { "water" }
            }));
        // neutral_tile has no AllowedBiomes (universal/neutral)
        _tileRegistry.RegisterTile(new TileDefinition(
            "neutral_tile",
            "Neutral Tile",
            TilePassability.Passable,
            Vector2I.Zero));

        // Create test registry with biomes that have distinct signatures
        _registry = new BiomeRegistry();

        var fireSignature = new CardSignature(new[] { 0f, 1f, 0f, 0f, 0f, 0f, 0f, 0f }); // High Febris (fire)
        var waterSignature = new CardSignature(new[] { 0f, -1f, 0f, 0f, 0f, 0f, 0f, 0f }); // Low Febris (water)

        var firePool = new TilePool();
        firePool.Add("fire_tile", 1.0f);

        var waterPool = new TilePool();
        waterPool.Add("water_tile", 1.0f);

        var emptyPool = new TilePool();

        _registry.Register(new BiomeDefinition("fire", fireSignature, firePool, emptyPool));
        _registry.Register(new BiomeDefinition("water", waterSignature, waterPool, emptyPool));
    }
}
