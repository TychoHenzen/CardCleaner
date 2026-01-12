using System.Collections.Generic;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh.Biomes;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.Wfc.Constraints;

/// <summary>
/// Constraint that uses card-based gradient to influence tile selection.
/// Tiles with higher affinity to the local signature get boosted weights.
/// </summary>
/// <remarks>
/// Affinity is computed by comparing the tile's preferred signature (if any)
/// to the gradient signature at the vertex position. Tiles without a preferred
/// signature get neutral weighting.
///
/// Signature dimensions (from CardSignature):
/// [0] Solidum (solid vs air) - affects ground vs gap
/// [1] Febris (cold vs hot) - affects water/ice vs sand/fire
/// [2] Ordinem (chaos vs order) - affects variation
/// [3] Lumines (dark vs light) - affects dark vs light terrain
/// [4] Varias (time vs space) - affects variation
/// [5] Inertiae (heavy vs light) - affects rock vs grass
/// [6] Subsidium (harmful vs helpful) - affects hazards
/// [7] Spatium (near vs far) - affects density
/// </remarks>
public class MeshBiomeAffinityConstraint : IMeshWfcConstraint
{
    private readonly MeshCardBasedGradient _gradient;
    private readonly MeshWfcGrid _grid;
    private readonly Dictionary<string, TileAffinity> _tileAffinities;

    /// <summary>
    /// Factor controlling how much signature affinity affects probability.
    /// Higher values create sharper biome boundaries.
    /// </summary>
    public float BoostFactor { get; set; } = 3.0f;

    /// <summary>
    /// Minimum weight modifier to prevent complete elimination.
    /// </summary>
    public float MinModifier { get; set; } = 0.1f;

    /// <summary>
    /// Creates a biome affinity constraint.
    /// </summary>
    /// <param name="gradient">The card-based gradient for signature lookup.</param>
    /// <param name="grid">The mesh grid (for vertex position lookup).</param>
    /// <param name="tileAffinities">Mapping of tile IDs to their preferred signatures.</param>
    public MeshBiomeAffinityConstraint(
        MeshCardBasedGradient gradient,
        MeshWfcGrid grid,
        Dictionary<string, TileAffinity>? tileAffinities = null)
    {
        _gradient = gradient;
        _grid = grid;
        _tileAffinities = tileAffinities ?? new Dictionary<string, TileAffinity>();
    }

    /// <summary>
    /// Adds or updates a tile's affinity definition.
    /// </summary>
    public void SetTileAffinity(string tileId, TileAffinity affinity)
    {
        _tileAffinities[tileId] = affinity;
    }

    /// <summary>
    /// Creates default affinities based on tile naming conventions.
    /// </summary>
    public void SetDefaultAffinities(IEnumerable<string> tileIds)
    {
        foreach (var tileId in tileIds)
        {
            var lowerTile = tileId.ToLowerInvariant();

            // Temperature-based tiles (dimension 1: Febris)
            if (lowerTile.Contains("water") || lowerTile.Contains("ice"))
            {
                _tileAffinities[tileId] = TileAffinity.FromDimension(1, -0.5f);
            }
            else if (lowerTile.Contains("sand") || lowerTile.Contains("fire") || lowerTile.Contains("lava"))
            {
                _tileAffinities[tileId] = TileAffinity.FromDimension(1, 0.5f);
            }
            // Density-based tiles (dimension 5: Inertiae)
            else if (lowerTile.Contains("rock") || lowerTile.Contains("stone"))
            {
                _tileAffinities[tileId] = TileAffinity.FromDimension(5, 0.5f);
            }
            else if (lowerTile.Contains("grass") || lowerTile.Contains("plant"))
            {
                _tileAffinities[tileId] = TileAffinity.FromDimension(5, -0.5f);
            }
            // Light-based tiles (dimension 3: Lumines)
            else if (lowerTile.Contains("dark") || lowerTile.Contains("shadow"))
            {
                _tileAffinities[tileId] = TileAffinity.FromDimension(3, -0.5f);
            }
            else if (lowerTile.Contains("light") || lowerTile.Contains("bright"))
            {
                _tileAffinities[tileId] = TileAffinity.FromDimension(3, 0.5f);
            }
        }
    }

    public float GetWeightModifier(string tileId, int vertexId, MeshWfcGrid grid)
    {
        // Tiles without affinity get neutral weighting
        if (!_tileAffinities.TryGetValue(tileId, out var affinity))
            return 1.0f;

        // Get vertex position from mesh
        var vertex = _grid.Mesh.GetVertex(vertexId);
        if (vertex == null)
            return 1.0f;

        // Get signature at this position
        var localSignature = _gradient.GetSignatureAt(vertex.Position);

        // Calculate affinity score (how well this tile matches the local signature)
        var affinityScore = affinity.CalculateAffinity(localSignature);

        // Convert to weight modifier
        // affinityScore +1 → strong boost
        // affinityScore 0 → neutral
        // affinityScore -1 → penalty
        var modifier = 1.0f + affinityScore * BoostFactor;

        return Mathf.Max(MinModifier, modifier);
    }

    public void OnTileCollapsed(int vertexId, string tileId, MeshWfcGrid grid)
    {
        // No state to track
    }

    public void Reset()
    {
        // No state to reset
    }
}

/// <summary>
/// Defines a tile's preferred signature characteristics.
/// </summary>
public class TileAffinity
{
    /// <summary>
    /// Preferred signature values. null dimensions are ignored.
    /// </summary>
    public float?[] PreferredValues { get; }

    /// <summary>
    /// Weights for each dimension when calculating affinity.
    /// Higher weights mean stronger influence on tile selection.
    /// </summary>
    public float[] DimensionWeights { get; }

    public TileAffinity(float?[] preferredValues, float[]? dimensionWeights = null)
    {
        PreferredValues = preferredValues;
        DimensionWeights = dimensionWeights ?? new float[] { 1, 1, 1, 1, 1, 1, 1, 1 };
    }

    /// <summary>
    /// Creates an affinity that prefers a specific value in one dimension.
    /// </summary>
    public static TileAffinity FromDimension(int dimension, float preferredValue, float weight = 1.0f)
    {
        var values = new float?[8];
        values[dimension] = preferredValue;

        var weights = new float[8];
        weights[dimension] = weight;

        return new TileAffinity(values, weights);
    }

    /// <summary>
    /// Calculates how well a signature matches this affinity.
    /// Returns value in [-1, 1] where 1 = perfect match.
    /// </summary>
    public float CalculateAffinity(CardSignature signature)
    {
        var totalWeight = 0f;
        var weightedScore = 0f;

        for (var i = 0; i < 8; i++)
        {
            if (!PreferredValues[i].HasValue)
                continue;

            var preferred = PreferredValues[i]!.Value;
            var actual = signature[i];
            var weight = DimensionWeights[i];

            // Score is 1 - |difference|, scaled by weight
            // Perfect match (diff=0) = 1, opposite (diff=2) = -1
            var diff = Mathf.Abs(actual - preferred);
            var dimensionScore = 1.0f - diff;

            weightedScore += dimensionScore * weight;
            totalWeight += weight;
        }

        if (totalWeight <= 0)
            return 0f;

        return weightedScore / totalWeight;
    }
}
