using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Enumeration;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen;

/// <summary>
/// Manages cross-layer constraint modifications during WFC generation
/// </summary>
public class ConstraintManager
{
    private readonly List<(Vector3I position, LayerConstraint constraint)> _activeConstraints = new();

    /// <summary>
    /// Apply constraint modifications from a placed tile
    /// </summary>
    public void ApplyTileConstraints(Vector3I position, SemanticTile tile)
    {
        if (!tile.HasLayerConstraints) return;

        foreach (var constraint in tile.LayerConstraints)
        {
            var targetPosition = GetPositionInDirection(position, constraint.AffectedSocket);
            _activeConstraints.Add((targetPosition, constraint));
        }
    }

    /// <summary>
    /// Get active constraints for a position and layer
    /// </summary>
    public IEnumerable<LayerConstraint> GetConstraints(Vector3I position, TileLayer layer)
    {
        return _activeConstraints
            .Where(c => c.position == position && c.constraint.targetLayer == layer)
            .Select(c => c.constraint);
    }

    /// <summary>
    /// Clear all constraints
    /// </summary>
    public void Clear()
    {
        _activeConstraints.Clear();
    }

    private static Vector3I GetPositionInDirection(Vector3I position, Direction direction)
    {
        return direction switch
        {
            Direction.North => position + Vector3I.Back,
            Direction.East => position + Vector3I.Right,
            Direction.South => position + Vector3I.Forward,
            Direction.West => position + Vector3I.Left,
            Direction.NorthEast => position + Vector3I.Back + Vector3I.Right,
            Direction.SouthEast => position + Vector3I.Forward + Vector3I.Right,
            Direction.SouthWest => position + Vector3I.Forward + Vector3I.Left,
            Direction.NorthWest => position + Vector3I.Back + Vector3I.Left,
            Direction.Up => position + Vector3I.Up,
            Direction.Down => position + Vector3I.Down,
            _ => position
        };
    }
}