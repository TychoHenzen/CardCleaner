using System.Collections.Generic;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.Wfc.Constraints;

/// <summary>
/// Interface for constraints that modify tile selection weights during mesh WFC.
/// Constraints can boost or penalize specific tiles based on local context.
/// </summary>
public interface IMeshWfcConstraint
{
    /// <summary>
    /// Returns a weight modifier for the given tile at the specified vertex.
    /// The modifier is multiplied with the tile's base weight during selection.
    /// </summary>
    /// <param name="tileId">The tile being evaluated.</param>
    /// <param name="vertexId">The vertex where the tile would be placed.</param>
    /// <param name="grid">The current WFC grid state.</param>
    /// <returns>Weight modifier (1.0 = neutral, &gt;1.0 = boost, &lt;1.0 = penalty, 0.0 = forbidden).</returns>
    float GetWeightModifier(string tileId, int vertexId, MeshWfcGrid grid);

    /// <summary>
    /// Called when a tile is collapsed at a vertex.
    /// Allows constraints to update internal state.
    /// </summary>
    /// <param name="vertexId">The vertex that was collapsed.</param>
    /// <param name="tileId">The tile that was selected.</param>
    /// <param name="grid">The current WFC grid state.</param>
    void OnTileCollapsed(int vertexId, string tileId, MeshWfcGrid grid);

    /// <summary>
    /// Resets the constraint state for a new generation.
    /// </summary>
    void Reset();
}

/// <summary>
/// Context passed to mesh WFC constraints for decision making.
/// </summary>
public readonly struct MeshWfcConstraintContext
{
    public string TileId { get; init; }
    public int VertexId { get; init; }
    public MeshWfcGrid Grid { get; init; }

    /// <summary>
    /// Precomputed collapsed neighbor tiles for performance.
    /// Key is neighbor vertex ID, value is collapsed tile ID.
    /// </summary>
    public IReadOnlyDictionary<int, string>? CollapsedNeighbors { get; init; }
}
