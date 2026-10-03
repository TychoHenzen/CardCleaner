using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh.Decoration;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

/// <summary>
/// Renders decorations (trees, rocks, bushes, props) on irregular mesh quads.
/// Supports individual sprites, batched mesh rendering, and MultiMesh for efficiency.
/// </summary>
public partial class DecorationRenderer : Node2D
{
    private IrregularMesh? _mesh;
    private readonly Dictionary<int, DecorationData> _decorations = new();
    private readonly DecorationMultiMeshLayer _multiMeshLayer = new();

    private Node2D? _spriteContainer;

    /// <summary>
    /// Z-index for decoration layer (above terrain at Z=0).
    /// </summary>
    [Export]
    public int DecorationZIndex { get; set; } = 10;

    /// <summary>
    /// Random seed for decoration placement variation.
    /// </summary>
    [Export]
    public int RandomSeed { get; set; } = 42;

    public override void _Ready()
    {
        _spriteContainer = new Node2D { Name = "Decorations" };
        _spriteContainer.ZIndex = DecorationZIndex;
        AddChild(_spriteContainer);
    }

    /// <summary>
    /// Initialize the renderer with a mesh.
    /// </summary>
    public void Initialize(IrregularMesh mesh)
    {
        _mesh = mesh;
        Clear();
    }

    /// <summary>
    /// Check if a decoration can be placed at the given quad.
    /// </summary>
    /// <param name="quadId">The quad ID to check.</param>
    /// <param name="type">The decoration type to place.</param>
    /// <returns>True if placement is valid.</returns>
    public bool CanPlaceDecoration(int quadId, DecorationType type)
    {
        if (_mesh == null || quadId < 0 || quadId >= _mesh.Quads.Count)
            return false;

        var quad = _mesh.Quads[quadId];

        // Already has a decoration
        if (_decorations.ContainsKey(quadId))
            return false;

        // Check if quad is passable for non-blocking decorations
        if (!DecorationTerrainRules.IsCompatible(_mesh, quad, type))
            return false;

        return true;
    }

    /// <summary>
    /// Place a decoration at a quad centroid.
    /// </summary>
    /// <param name="quadId">The quad to decorate.</param>
    /// <param name="type">The decoration type.</param>
    /// <param name="variation">Optional variation index for multi-sprite decorations.</param>
    /// <returns>True if placement succeeded.</returns>
    public bool PlaceDecoration(int quadId, DecorationType type, int variation = 0)
    {
        if (!CanPlaceDecoration(quadId, type))
            return false;

        var quad = _mesh!.Quads[quadId];
        var data = new DecorationData
        {
            QuadId = quadId,
            Type = type,
            Variation = variation,
            Position = quad.Centroid,
            Rotation = 0f,
            Scale = Vector2.One
        };

        _decorations[quadId] = data;
        return true;
    }

    /// <summary>
    /// Place a decoration with random variation and slight position offset.
    /// </summary>
    public bool PlaceDecorationRandomized(int quadId, DecorationType type, int maxVariations = 1)
    {
        if (!CanPlaceDecoration(quadId, type))
            return false;

        var quad = _mesh!.Quads[quadId];

        // Random variation based on quad position for determinism
        var posHash = (int)(quad.Centroid.X * 73 + quad.Centroid.Y * 179) + RandomSeed;
        var localRng = new Random(posHash);

        var data = new DecorationData
        {
            QuadId = quadId,
            Type = type,
            Variation = localRng.Next(maxVariations),
            Position = quad.Centroid + new Vector2(
                (float)(localRng.NextDouble() - 0.5) * 4f,
                (float)(localRng.NextDouble() - 0.5) * 4f
            ),
            Rotation = (float)(localRng.NextDouble() * Mathf.Pi * 2),
            Scale = Vector2.One * (0.9f + (float)localRng.NextDouble() * 0.2f)
        };

        _decorations[quadId] = data;
        return true;
    }

    /// <summary>
    /// Remove a decoration from a quad.
    /// </summary>
    public bool RemoveDecoration(int quadId)
    {
        return _decorations.Remove(quadId);
    }

    /// <summary>
    /// Get decoration data for a quad.
    /// </summary>
    public DecorationData? GetDecoration(int quadId)
    {
        return _decorations.TryGetValue(quadId, out var data) ? data : null;
    }

    /// <summary>
    /// Render all decorations as individual sprites.
    /// </summary>
    /// <param name="textureProvider">Function to get texture for decoration type and variation.</param>
    public void RenderAsSprites(Func<DecorationType, int, Texture2D?> textureProvider)
    {
        if (_spriteContainer == null)
            return;

        // Clear existing sprites
        foreach (var child in _spriteContainer.GetChildren())
            child.QueueFree();

        // Create sprites for each decoration
        foreach (var data in _decorations.Values)
        {
            var texture = textureProvider(data.Type, data.Variation);
            if (texture == null)
                continue;

            var sprite = new Sprite2D
            {
                Texture = texture,
                Position = data.Position,
                Rotation = data.Rotation,
                Scale = data.Scale,
                Name = $"Decoration_{data.QuadId}"
            };

            _spriteContainer.AddChild(sprite);
        }
    }

    /// <summary>
    /// Render decorations of the same type using MultiMesh for efficiency.
    /// </summary>
    /// <param name="type">The decoration type to render.</param>
    /// <param name="mesh">The quad mesh to use for instances.</param>
    /// <param name="texture">The texture to apply.</param>
    public void RenderAsMultiMesh(DecorationType type, Mesh mesh, Texture2D texture)
    {
        _multiMeshLayer.Render(this, _decorations.Values, type, mesh, texture, DecorationZIndex);
    }

    /// <summary>
    /// Auto-populate decorations based on terrain types.
    /// </summary>
    /// <param name="terrainToDecoration">Mapping of terrain type to decoration type and probability.</param>
    public void AutoPopulate(
        Dictionary<int, (DecorationType type, float probability, int maxVariations)> terrainToDecoration)
    {
        if (_mesh == null)
            return;

        var rng = new Random(RandomSeed);

        foreach (var quad in _mesh.Quads)
        {
            // Get dominant terrain type from corners
            var terrainCounts = new Dictionary<int, int>();
            foreach (var vertexId in quad.VertexIds)
            {
                var terrain = _mesh.Vertices[vertexId].TerrainType;
                terrainCounts[terrain] = terrainCounts.GetValueOrDefault(terrain) + 1;
            }
            var dominantTerrain = terrainCounts.MaxBy(kv => kv.Value).Key;

            if (!terrainToDecoration.TryGetValue(dominantTerrain, out var config))
                continue;

            if (rng.NextDouble() > config.probability)
                continue;

            PlaceDecorationRandomized(quad.Id, config.type, config.maxVariations);
        }
    }

    /// <summary>
    /// Clear all decorations and visuals.
    /// </summary>
    public void Clear()
    {
        _decorations.Clear();

        if (_spriteContainer != null)
        {
            foreach (var child in _spriteContainer.GetChildren())
                child.QueueFree();
        }

        _multiMeshLayer.Clear();
    }

    /// <summary>
    /// Get all quads with decorations.
    /// </summary>
    public IEnumerable<int> GetDecoratedQuads() => _decorations.Keys;

    /// <summary>
    /// Get decoration count.
    /// </summary>
    public int DecorationCount => _decorations.Count;
}

/// <summary>
/// Data for a placed decoration.
/// </summary>
public class DecorationData
{
    public int QuadId { get; init; }
    public DecorationType Type { get; init; }
    public int Variation { get; init; }
    public Vector2 Position { get; init; }
    public float Rotation { get; init; }
    public Vector2 Scale { get; init; }
}
