using System.Collections.Generic;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh.Wfc.Constraints;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;

/// <summary>
/// Adapter that wraps an IWfcConstraint (regular grid) to work with the unified interface.
/// Only works correctly when the grid is a WfcGrid or implements IWfcGridWithCoordinates.
/// </summary>
public class GridConstraintAdapter : IUnifiedWfcConstraint
{
    private readonly IWfcConstraint _constraint;

    public GridConstraintAdapter(IWfcConstraint constraint)
    {
        _constraint = constraint;
    }

    public float GetWeightModifier(int cellId, string tileId, IWfcGrid grid)
    {
        // Need to convert to WfcConstraintContext, which requires grid coordinates
        if (grid is not IWfcGridWithCoordinates gridWithCoords)
        {
            // Can't use position-based constraint with non-coordinate grid
            return 1.0f;
        }

        if (grid is not WfcGrid wfcGrid)
        {
            // Need the actual WfcGrid type for the context
            return 1.0f;
        }

        var position = gridWithCoords.CellIdToPosition(cellId);
        var context = new WfcConstraintContext
        {
            Position = position,
            TileId = tileId,
            Grid = wfcGrid,
            Rng = null
        };

        return _constraint.GetProbabilityModifier(context);
    }

    public void OnTileCollapsed(int cellId, string tileId, IWfcGrid grid)
    {
        // IWfcConstraint doesn't have an OnTileCollapsed callback
        // If the underlying constraint needs state updates, it must handle this elsewhere
    }

    public void Reset()
    {
        // IWfcConstraint doesn't have a Reset method
    }
}

/// <summary>
/// Adapter that wraps an IMeshWfcConstraint to work with the unified interface.
/// Only works correctly when the grid is a MeshWfcGrid.
/// </summary>
public class MeshConstraintAdapter : IUnifiedWfcConstraint
{
    private readonly IMeshWfcConstraint _constraint;

    public MeshConstraintAdapter(IMeshWfcConstraint constraint)
    {
        _constraint = constraint;
    }

    public float GetWeightModifier(int cellId, string tileId, IWfcGrid grid)
    {
        if (grid is not MeshWfcGrid meshGrid)
        {
            // Can't use mesh constraint with non-mesh grid
            return 1.0f;
        }

        return _constraint.GetWeightModifier(tileId, cellId, meshGrid);
    }

    public void OnTileCollapsed(int cellId, string tileId, IWfcGrid grid)
    {
        if (grid is MeshWfcGrid meshGrid)
        {
            _constraint.OnTileCollapsed(cellId, tileId, meshGrid);
        }
    }

    public void Reset()
    {
        _constraint.Reset();
    }
}

/// <summary>
/// Extension methods for converting existing constraints to unified interface.
/// </summary>
public static class ConstraintExtensions
{
    /// <summary>
    /// Wraps a grid constraint in a unified adapter.
    /// </summary>
    public static IUnifiedWfcConstraint ToUnified(this IWfcConstraint constraint)
        => new GridConstraintAdapter(constraint);

    /// <summary>
    /// Wraps a mesh constraint in a unified adapter.
    /// </summary>
    public static IUnifiedWfcConstraint ToUnified(this IMeshWfcConstraint constraint)
        => new MeshConstraintAdapter(constraint);
}
