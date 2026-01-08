using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Worldgen;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Tests.Core.PropertyTesting;
using CardCleaner.Tests.Core.PropertyTesting.Generators;
using CardCleaner.Tests.Mocks;
using FsCheck;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.Properties;

/// <summary>
///     Property-based tests for map generation: connectivity, borders, and transitions.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public partial class MapGenerationProperties : PropertyTestBase
{
    private BiomeRegistry _registry = null!;
    private MockTileRegistry _tileRegistry = null!;

    [BeforeTest]
    public new void SetupPropertyTest()
    {
        base.SetupPropertyTest();
        CardSignatureArbitrary.Register();
        _registry = new BiomeRegistry();
        _registry.RegisterDefaultBiomes();
        _tileRegistry = MockTileRegistry.CreateWithTestTiles();
    }

    #region Map Connectivity Properties (ST008)

    [TestCase]
    public void Map_AllPassableTilesAreConnected()
    {
        var mapParamsGen =
            from seed in Gen.Choose(1, 1000000)
            from width in Gen.Choose(8, 15)
            from height in Gen.Choose(8, 15)
            select (Seed: seed, Width: width, Height: height);

        Property(p => p
            .ForAll(
                Arb.From(mapParamsGen),
                args =>
                {
                    var map = GenerateMapWithSeed((ulong)args.Seed, new Vector2I(args.Width, args.Height));
                    return AllPassableTilesConnected(map);
                })
            .Iterations(50));
    }

    [TestCase]
    public void Map_PlayerStartIsOnPassableTile()
    {
        var mapParamsGen =
            from seed in Gen.Choose(1, 1000000)
            from width in Gen.Choose(8, 15)
            from height in Gen.Choose(8, 15)
            select (Seed: seed, Width: width, Height: height);

        Property(p => p
            .ForAll(
                Arb.From(mapParamsGen),
                args =>
                {
                    var map = GenerateMapWithSeed((ulong)args.Seed, new Vector2I(args.Width, args.Height));
                    return map.PassableTiles.Contains(map.PlayerStart);
                })
            .Iterations(50));
    }

    [TestCase]
    public void Map_EnemyPositionsAreOnPassableTiles()
    {
        var mapParamsGen =
            from seed in Gen.Choose(1, 1000000)
            from width in Gen.Choose(10, 15)
            from height in Gen.Choose(10, 15)
            select (Seed: seed, Width: width, Height: height);

        Property(p => p
            .ForAll(
                Arb.From(mapParamsGen),
                args =>
                {
                    var map = GenerateMapWithSeed((ulong)args.Seed, new Vector2I(args.Width, args.Height));
                    return map.EnemyPositions.All(pos => map.PassableTiles.Contains(pos));
                })
            .Iterations(50));
    }

    [TestCase]
    public void Map_EnemyPositionsAreDistinctFromPlayerStart()
    {
        var mapParamsGen =
            from seed in Gen.Choose(1, 1000000)
            from width in Gen.Choose(10, 15)
            from height in Gen.Choose(10, 15)
            select (Seed: seed, Width: width, Height: height);

        Property(p => p
            .ForAll(
                Arb.From(mapParamsGen),
                args =>
                {
                    var map = GenerateMapWithSeed((ulong)args.Seed, new Vector2I(args.Width, args.Height));
                    return !map.EnemyPositions.Contains(map.PlayerStart);
                })
            .Iterations(50));
    }

    [TestCase]
    public void Map_HasAtLeastOnePassableTile()
    {
        var mapParamsGen =
            from seed in Gen.Choose(1, 1000000)
            from width in Gen.Choose(5, 10)
            from height in Gen.Choose(5, 10)
            select (Seed: seed, Width: width, Height: height);

        Property(p => p
            .ForAll(
                Arb.From(mapParamsGen),
                args =>
                {
                    var map = GenerateMapWithSeed((ulong)args.Seed, new Vector2I(args.Width, args.Height));
                    return map.PassableTiles.Count > 0;
                })
            .Iterations(50));
    }

    [TestCase]
    public void Map_DimensionsMatchRequested()
    {
        var mapParamsGen =
            from seed in Gen.Choose(1, 1000000)
            from width in Gen.Choose(5, 20)
            from height in Gen.Choose(5, 20)
            select (Seed: seed, Width: width, Height: height);

        Property(p => p
            .ForAll(
                Arb.From(mapParamsGen),
                args =>
                {
                    var requestedSize = new Vector2I(args.Width, args.Height);
                    var map = GenerateMapWithSeed((ulong)args.Seed, requestedSize);
                    return map.Size == requestedSize &&
                           map.TileIds.GetLength(0) == args.Height &&
                           map.TileIds.GetLength(1) == args.Width;
                })
            .Iterations(50));
    }

    #endregion

    #region Border and Transition Properties (ST006 & ST007)

    [TestCase]
    public void Map_AllTileIdsAreNonNull()
    {
        var mapParamsGen =
            from seed in Gen.Choose(1, 1000000)
            from width in Gen.Choose(5, 15)
            from height in Gen.Choose(5, 15)
            select (Seed: seed, Width: width, Height: height);

        Property(p => p
            .ForAll(
                Arb.From(mapParamsGen),
                args =>
                {
                    var map = GenerateMapWithSeed((ulong)args.Seed, new Vector2I(args.Width, args.Height));

                    for (var y = 0; y < args.Height; y++)
                    for (var x = 0; x < args.Width; x++)
                        if (map.TileIds[y, x] == null)
                            return false;

                    return true;
                })
            .Iterations(50));
    }

    [TestCase]
    public void Map_DecorationOverlaysHaveValidBitmasks()
    {
        var mapParamsGen =
            from seed in Gen.Choose(1, 1000000)
            from width in Gen.Choose(8, 15)
            from height in Gen.Choose(8, 15)
            select (Seed: seed, Width: width, Height: height);

        Property(p => p
            .ForAll(
                Arb.From(mapParamsGen),
                args =>
                {
                    var map = GenerateMapWithSeed((ulong)args.Seed, new Vector2I(args.Width, args.Height));

                    // All decoration overlays should have valid 4-bit bitmasks (0-15)
                    foreach (var (_, (_, _, bitmask)) in map.DecorationOverlays)
                    {
                        if (bitmask < 0 || bitmask > 15)
                            return false;
                    }

                    return true;
                })
            .Iterations(50));
    }

    [TestCase]
    public void Map_DecorationOverlaysAreWithinMapBounds()
    {
        var mapParamsGen =
            from seed in Gen.Choose(1, 1000000)
            from width in Gen.Choose(8, 15)
            from height in Gen.Choose(8, 15)
            select (Seed: seed, Width: width, Height: height);

        Property(p => p
            .ForAll(
                Arb.From(mapParamsGen),
                args =>
                {
                    var map = GenerateMapWithSeed((ulong)args.Seed, new Vector2I(args.Width, args.Height));

                    // Visual grid is (size+1) in each dimension for dual-grid auto-tiling
                    var visualWidth = args.Width + 1;
                    var visualHeight = args.Height + 1;
                    foreach (var (position, _) in map.DecorationOverlays)
                    {
                        if (position.X < 0 || position.X >= visualWidth ||
                            position.Y < 0 || position.Y >= visualHeight)
                            return false;
                    }

                    return true;
                })
            .Iterations(50));
    }

    [TestCase]
    public void Map_BiomeMapMatchesDimensions()
    {
        var mapParamsGen =
            from seed in Gen.Choose(1, 1000000)
            from width in Gen.Choose(5, 15)
            from height in Gen.Choose(5, 15)
            select (Seed: seed, Width: width, Height: height);

        Property(p => p
            .ForAll(
                Arb.From(mapParamsGen),
                args =>
                {
                    var map = GenerateMapWithSeed((ulong)args.Seed, new Vector2I(args.Width, args.Height));
                    return map.BiomeMap.GetLength(0) == args.Height &&
                           map.BiomeMap.GetLength(1) == args.Width;
                })
            .Iterations(50));
    }

    [TestCase]
    public void Map_PassableTilesAreWithinBounds()
    {
        var mapParamsGen =
            from seed in Gen.Choose(1, 1000000)
            from width in Gen.Choose(5, 15)
            from height in Gen.Choose(5, 15)
            select (Seed: seed, Width: width, Height: height);

        Property(p => p
            .ForAll(
                Arb.From(mapParamsGen),
                args =>
                {
                    var map = GenerateMapWithSeed((ulong)args.Seed, new Vector2I(args.Width, args.Height));

                    foreach (var pos in map.PassableTiles)
                    {
                        if (pos.X < 0 || pos.X >= args.Width ||
                            pos.Y < 0 || pos.Y >= args.Height)
                            return false;
                    }

                    return true;
                })
            .Iterations(50));
    }

    #endregion

    #region Integration Tests with Signatures (ST009)

    [TestCase]
    public void Map_WithExtremeNegativeSignature_RemainsValid()
    {
        var mapParamsGen =
            from seed in Gen.Choose(1, 1000000)
            from width in Gen.Choose(8, 15)
            from height in Gen.Choose(8, 15)
            select (Seed: seed, Width: width, Height: height);

        Property(p => p
            .ForAll(
                Arb.From(mapParamsGen),
                args =>
                {
                    // All -1 signature (extreme cold, dark, chaotic, etc.)
                    var signature = new CardSignature(new[] { -1f, -1f, -1f, -1f, -1f, -1f, -1f, -1f });
                    var map = GenerateMapWithSignature((ulong)args.Seed, new Vector2I(args.Width, args.Height), signature);
                    return ValidateMapInvariants(map, args.Width, args.Height);
                })
            .Iterations(30));
    }

    [TestCase]
    public void Map_WithExtremePositiveSignature_RemainsValid()
    {
        var mapParamsGen =
            from seed in Gen.Choose(1, 1000000)
            from width in Gen.Choose(8, 15)
            from height in Gen.Choose(8, 15)
            select (Seed: seed, Width: width, Height: height);

        Property(p => p
            .ForAll(
                Arb.From(mapParamsGen),
                args =>
                {
                    // All +1 signature (extreme hot, bright, orderly, etc.)
                    var signature = new CardSignature(new[] { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f });
                    var map = GenerateMapWithSignature((ulong)args.Seed, new Vector2I(args.Width, args.Height), signature);
                    return ValidateMapInvariants(map, args.Width, args.Height);
                })
            .Iterations(30));
    }

    [TestCase]
    public void Map_WithNeutralSignature_RemainsValid()
    {
        var mapParamsGen =
            from seed in Gen.Choose(1, 1000000)
            from width in Gen.Choose(8, 15)
            from height in Gen.Choose(8, 15)
            select (Seed: seed, Width: width, Height: height);

        Property(p => p
            .ForAll(
                Arb.From(mapParamsGen),
                args =>
                {
                    // All 0 signature (neutral/balanced)
                    var signature = new CardSignature();
                    var map = GenerateMapWithSignature((ulong)args.Seed, new Vector2I(args.Width, args.Height), signature);
                    return ValidateMapInvariants(map, args.Width, args.Height);
                })
            .Iterations(30));
    }

    [TestCase]
    public void Map_WithRandomSignature_RemainsValid()
    {
        var mapParamsGen =
            from seed in Gen.Choose(1, 1000000)
            from width in Gen.Choose(8, 15)
            from height in Gen.Choose(8, 15)
            from signature in CardSignatureArbitrary.Generator
            select (Seed: seed, Width: width, Height: height, Signature: signature);

        Property(p => p
            .ForAll(
                Arb.From(mapParamsGen),
                args =>
                {
                    var map = GenerateMapWithSignature((ulong)args.Seed, new Vector2I(args.Width, args.Height), args.Signature);
                    return ValidateMapInvariants(map, args.Width, args.Height);
                })
            .Iterations(50));
    }

    [TestCase]
    public void Map_WithMixedExtremeSignature_RemainsValid()
    {
        var mapParamsGen =
            from seed in Gen.Choose(1, 1000000)
            from width in Gen.Choose(8, 15)
            from height in Gen.Choose(8, 15)
            select (Seed: seed, Width: width, Height: height);

        Property(p => p
            .ForAll(
                Arb.From(mapParamsGen),
                args =>
                {
                    // Alternating extremes: -1, +1, -1, +1, etc.
                    var signature = new CardSignature(new[] { -1f, 1f, -1f, 1f, -1f, 1f, -1f, 1f });
                    var map = GenerateMapWithSignature((ulong)args.Seed, new Vector2I(args.Width, args.Height), signature);
                    return ValidateMapInvariants(map, args.Width, args.Height);
                })
            .Iterations(30));
    }

    /// <summary>
    ///     Validates all core map invariants that should hold regardless of input signature.
    /// </summary>
    private bool ValidateMapInvariants(SimpleMapData map, int expectedWidth, int expectedHeight)
    {
        // Dimension invariants
        if (map.Size.X != expectedWidth || map.Size.Y != expectedHeight)
            return false;

        if (map.TileIds.GetLength(0) != expectedHeight || map.TileIds.GetLength(1) != expectedWidth)
            return false;

        if (map.BiomeMap.GetLength(0) != expectedHeight || map.BiomeMap.GetLength(1) != expectedWidth)
            return false;

        // Has at least one passable tile
        if (map.PassableTiles.Count == 0)
            return false;

        // Player start is valid
        if (!map.PassableTiles.Contains(map.PlayerStart))
            return false;

        // Enemy positions are valid
        if (map.EnemyPositions.Any(pos => !map.PassableTiles.Contains(pos)))
            return false;

        if (map.EnemyPositions.Contains(map.PlayerStart))
            return false;

        // All tile IDs are non-null
        for (var y = 0; y < expectedHeight; y++)
        for (var x = 0; x < expectedWidth; x++)
            if (map.TileIds[y, x] == null)
                return false;

        // All passable tiles are within bounds
        if (map.PassableTiles.Any(pos => pos.X < 0 || pos.X >= expectedWidth || pos.Y < 0 || pos.Y >= expectedHeight))
            return false;

        // Decoration overlays are valid (visual grid is size+1 in each dimension)
        var visualWidth = expectedWidth + 1;
        var visualHeight = expectedHeight + 1;
        foreach (var (position, (_, _, bitmask)) in map.DecorationOverlays)
        {
            if (position.X < 0 || position.X >= visualWidth || position.Y < 0 || position.Y >= visualHeight)
                return false;

            if (bitmask < 0 || bitmask > 15)
                return false;
        }

        // All passable tiles are connected
        if (!AllPassableTilesConnected(map))
            return false;

        return true;
    }

    #endregion

    #region Helper Methods

    /// <summary>
    ///     Simple gradient that returns a constant signature at all positions.
    /// </summary>
    private sealed partial class ConstantGradient : BaselineGradient
    {
        private readonly CardSignature _signature;

        public ConstantGradient(CardSignature signature)
        {
            _signature = signature;
        }

        public override CardSignature GetSignatureAt(Vector2I position, Vector2I mapSize) => _signature;
    }

    private SimpleMapData GenerateMapWithSeed(ulong seed, Vector2I size)
    {
        return GenerateMapWithSignature(seed, size, new CardSignature());
    }

    private SimpleMapData GenerateMapWithSignature(ulong seed, Vector2I size, CardSignature signature)
    {
        var rng = new RandomNumberGenerator { Seed = seed };
        var gradient = new ConstantGradient(signature);
        var biomeProvider = new BiomeMapGenerator(_registry, gradient, size);
        var generator = new SimpleMapGenerator(rng, biomeProvider, _tileRegistry, _tileRegistry);
        return generator.GenerateMap(size);
    }

    private static bool AllPassableTilesConnected(SimpleMapData map)
    {
        if (map.PassableTiles.Count <= 1)
            return true;

        var visited = new HashSet<Vector2I>();
        var queue = new Queue<Vector2I>();

        // Start flood fill from first passable tile
        queue.Enqueue(map.PassableTiles[0]);
        visited.Add(map.PassableTiles[0]);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            // Check 4 cardinal neighbors
            var neighbors = new[]
            {
                current + Vector2I.Up,
                current + Vector2I.Right,
                current + Vector2I.Down,
                current + Vector2I.Left
            };

            foreach (var neighbor in neighbors)
            {
                if (visited.Contains(neighbor))
                    continue;

                if (map.PassableTiles.Contains(neighbor))
                {
                    visited.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }
        }

        // All passable tiles should be visited
        return visited.Count == map.PassableTiles.Count;
    }

    #endregion
}
