using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.WeightModifiers;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Structures;

/// <summary>
/// Placement result containing position and structure information.
/// </summary>
public sealed class StructurePlacement
{
    public required Vector2I Position { get; init; }
    public required StructureResult Result { get; init; }
    public required string StructureId { get; init; }
}

/// <summary>
/// Service that orchestrates structure placement on maps.
/// Finds valid positions, places structures, and registers them with the proximity modifier.
/// </summary>
public sealed class StructurePlacer
{
    private readonly StructureProximityModifier? _proximityModifier;
    private readonly List<StructurePlacement> _placedStructures = [];

    /// <summary>
    /// Maximum attempts to find a valid position for a structure.
    /// </summary>
    public int MaxPlacementAttempts { get; set; } = 50;

    public StructurePlacer(StructureProximityModifier? proximityModifier = null)
    {
        _proximityModifier = proximityModifier;
    }

    /// <summary>
    /// Get all structures placed during this generation session.
    /// </summary>
    public IReadOnlyList<StructurePlacement> PlacedStructures => _placedStructures;

    /// <summary>
    /// Clear all placed structures (call before new map generation).
    /// </summary>
    public void Clear()
    {
        _placedStructures.Clear();
        _proximityModifier?.Clear();
    }

    /// <summary>
    /// Find valid positions for a stamp on the map.
    /// </summary>
    /// <param name="stamp">Structure stamp to place.</param>
    /// <param name="mapSize">Map dimensions.</param>
    /// <param name="getBiomeAt">Function to get biome at position.</param>
    /// <param name="isPassableAt">Function to check if position is passable.</param>
    /// <param name="rng">Random number generator for shuffling.</param>
    /// <param name="maxResults">Maximum number of positions to return.</param>
    public List<Vector2I> FindValidPositions(
        StructureStamp stamp,
        Vector2I mapSize,
        Func<Vector2I, BiomeDefinition> getBiomeAt,
        Func<Vector2I, bool> isPassableAt,
        RandomNumberGenerator rng,
        int maxResults = 10)
    {
        ArgumentNullException.ThrowIfNull(stamp);
        ArgumentNullException.ThrowIfNull(getBiomeAt);
        ArgumentNullException.ThrowIfNull(isPassableAt);
        ArgumentNullException.ThrowIfNull(rng);

        var validPositions = new List<Vector2I>();

        // Generate candidate positions and shuffle
        var candidates = GenerateCandidatePositions(stamp.Size, mapSize, rng);

        foreach (var position in candidates)
        {
            if (IsValidPlacement(stamp, position, mapSize, getBiomeAt, isPassableAt))
            {
                validPositions.Add(position);
                if (validPositions.Count >= maxResults)
                    break;
            }
        }

        return validPositions;
    }

    /// <summary>
    /// Check if a stamp can be placed at a position.
    /// </summary>
    public bool IsValidPlacement(
        StructureStamp stamp,
        Vector2I position,
        Vector2I mapSize,
        Func<Vector2I, BiomeDefinition> getBiomeAt,
        Func<Vector2I, bool> isPassableAt)
    {
        // Check bounds
        if (position.X < 0 || position.Y < 0 ||
            position.X + stamp.Size.X > mapSize.X ||
            position.Y + stamp.Size.Y > mapSize.Y)
            return false;

        // Check biome at anchor position
        var biome = getBiomeAt(position);
        if (!stamp.IsBiomeAllowed(biome.Id))
            return false;

        // Check spacing from other structures
        if (!CheckSpacing(position, stamp.Size, stamp.MinSpacing))
            return false;

        // Check that all footprint tiles are passable (structure replaces passable terrain)
        for (var dy = 0; dy < stamp.Size.Y; dy++)
        {
            for (var dx = 0; dx < stamp.Size.X; dx++)
            {
                var checkPos = position + new Vector2I(dx, dy);
                if (!isPassableAt(checkPos))
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Place a stamp at a position and update the tile array.
    /// </summary>
    /// <param name="stamp">Structure stamp to place.</param>
    /// <param name="position">Anchor position (top-left).</param>
    /// <param name="tileIds">Tile ID array to update.</param>
    /// <param name="mapSize">Map dimensions for bounds checking.</param>
    /// <returns>True if placed successfully.</returns>
    public bool PlaceStamp(
        StructureStamp stamp,
        Vector2I position,
        string[,] tileIds,
        Vector2I mapSize)
    {
        ArgumentNullException.ThrowIfNull(stamp);
        ArgumentNullException.ThrowIfNull(tileIds);

        // Verify position is valid
        if (position.X < 0 || position.Y < 0 ||
            position.X + stamp.Size.X > mapSize.X ||
            position.Y + stamp.Size.Y > mapSize.Y)
            return false;

        // Place all tiles from the stamp
        var tiles = stamp.GetAllTiles();
        foreach (var (offset, tileId) in tiles)
        {
            var tilePos = position + offset;
            if (tilePos.X >= 0 && tilePos.X < mapSize.X &&
                tilePos.Y >= 0 && tilePos.Y < mapSize.Y)
            {
                tileIds[tilePos.Y, tilePos.X] = tileId;
            }
        }

        // Create result and register
        var result = StructureResult.FromStamp(stamp);
        RegisterPlacement(position, stamp.Id, result);

        return true;
    }

    /// <summary>
    /// Place a procedural structure at a position.
    /// </summary>
    public bool PlaceProceduralStructure(
        IProceduralStructure structure,
        Vector2I position,
        int seed,
        BiomeDefinition biome,
        IMapQuery mapQuery,
        string[,] tileIds,
        Vector2I mapSize)
    {
        ArgumentNullException.ThrowIfNull(structure);
        ArgumentNullException.ThrowIfNull(biome);
        ArgumentNullException.ThrowIfNull(mapQuery);
        ArgumentNullException.ThrowIfNull(tileIds);

        // Generate the structure
        var result = structure.Generate(position, seed, biome, mapQuery);

        // Verify bounds
        if (position.X < 0 || position.Y < 0 ||
            position.X + result.Size.X > mapSize.X ||
            position.Y + result.Size.Y > mapSize.Y)
            return false;

        // Place tiles
        foreach (var (offset, tileId) in result.Tiles)
        {
            var tilePos = position + offset;
            if (tilePos.X >= 0 && tilePos.X < mapSize.X &&
                tilePos.Y >= 0 && tilePos.Y < mapSize.Y)
            {
                tileIds[tilePos.Y, tilePos.X] = tileId;
            }
        }

        // Register placement
        RegisterPlacement(position, structure.Id, result);

        return true;
    }

    /// <summary>
    /// Try to place a structure at a random valid position.
    /// </summary>
    public bool TryPlaceRandom(
        StructureStamp stamp,
        Vector2I mapSize,
        Func<Vector2I, BiomeDefinition> getBiomeAt,
        Func<Vector2I, bool> isPassableAt,
        string[,] tileIds,
        RandomNumberGenerator rng)
    {
        var validPositions = FindValidPositions(stamp, mapSize, getBiomeAt, isPassableAt, rng, 1);

        if (validPositions.Count == 0)
            return false;

        return PlaceStamp(stamp, validPositions[0], tileIds, mapSize);
    }

    /// <summary>
    /// Check if a position respects minimum spacing from all placed structures.
    /// </summary>
    private bool CheckSpacing(Vector2I position, Vector2I size, int minSpacing)
    {
        foreach (var placed in _placedStructures)
        {
            var distance = CalculateMinDistance(
                position, size,
                placed.Position, placed.Result.Size);

            if (distance < minSpacing)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Calculate minimum distance between two axis-aligned rectangles.
    /// </summary>
    private static float CalculateMinDistance(Vector2I pos1, Vector2I size1, Vector2I pos2, Vector2I size2)
    {
        // Calculate gaps between rectangles
        var gapX = Math.Max(0, Math.Max(pos1.X - (pos2.X + size2.X), pos2.X - (pos1.X + size1.X)));
        var gapY = Math.Max(0, Math.Max(pos1.Y - (pos2.Y + size2.Y), pos2.Y - (pos1.Y + size1.Y)));

        // If overlapping in either axis, gap is 0
        if (gapX == 0 && gapY == 0)
            return 0; // Overlapping

        return MathF.Sqrt(gapX * gapX + gapY * gapY);
    }

    /// <summary>
    /// Register a placed structure for spacing and proximity calculations.
    /// </summary>
    private void RegisterPlacement(Vector2I position, string structureId, StructureResult result)
    {
        var placement = new StructurePlacement
        {
            Position = position,
            StructureId = structureId,
            Result = result
        };

        _placedStructures.Add(placement);

        // Register with proximity modifier if available
        if (_proximityModifier != null && result.TileAffinities != null)
        {
            var affinities = new Dictionary<string, float>();
            foreach (var entry in result.TileAffinities)
            {
                if (!string.IsNullOrEmpty(entry.TileId))
                    affinities[entry.TileId] = entry.Affinity;
            }

            _proximityModifier.RegisterStructure(new PlacedStructureInfluence
            {
                Position = position,
                Size = result.Size,
                InfluenceRadius = result.InfluenceRadius,
                TileAffinities = affinities
            });
        }
    }

    /// <summary>
    /// Generate shuffled candidate positions for structure placement.
    /// </summary>
    private static List<Vector2I> GenerateCandidatePositions(Vector2I structureSize, Vector2I mapSize, RandomNumberGenerator rng)
    {
        var candidates = new List<Vector2I>();

        // Generate all valid anchor positions
        for (var y = 0; y <= mapSize.Y - structureSize.Y; y++)
        {
            for (var x = 0; x <= mapSize.X - structureSize.X; x++)
            {
                candidates.Add(new Vector2I(x, y));
            }
        }

        // Fisher-Yates shuffle
        for (var i = candidates.Count - 1; i > 0; i--)
        {
            var j = rng.RandiRange(0, i);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }

        return candidates;
    }
}
