using System.Collections.Generic;
using CardCleaner.Scripts.Core.Enum;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen;

/// <summary>
/// Manages cross-layer constraint modifications during WFC generation
/// </summary>
public class ConstraintManager
{
    private readonly Dictionary<Vector3I, Dictionary<Direction, SocketType>> _activeConstraints = new();
    
    /// <summary>
    /// Apply constraint modifications from a placed tile
    /// </summary>
    public void ApplyTileConstraints(Vector3I position, SemanticTile tile)
    {
        if (!tile.HasLayerConstraints) return;
        
        foreach (var kvp in tile.LayerConstraints)
        {
            var targetPosition = GetPositionInDirection(position, kvp.Key);
            AddConstraint(targetPosition, GetOppositeDirection(kvp.Key), kvp.Value);
        }
    }
    
    /// <summary>
    /// Get active constraints for a position and layer
    /// </summary>
    public SocketType? GetConstraint(Vector3I position, Direction direction)
    {
        if (!_activeConstraints.TryGetValue(position, out var constraints)) return null;
        return constraints.TryGetValue(direction, out var constraint) ? constraint : null;
    }
    
    /// <summary>
    /// Clear all constraints
    /// </summary>
    public void Clear()
    {
        _activeConstraints.Clear();
    }
    
    private void AddConstraint(Vector3I position, Direction direction, SocketType constraint)
    {
        if (!_activeConstraints.ContainsKey(position))
            _activeConstraints[position] = new Dictionary<Direction, SocketType>();
            
        _activeConstraints[position][direction] = constraint;
    }
    
    private static Vector3I GetPositionInDirection(Vector3I position, Direction direction)
    {
        return direction switch
        {
            Direction.North => position + Vector3I.Back,
            Direction.East => position + Vector3I.Right,
            Direction.South => position + Vector3I.Forward,
            Direction.West => position + Vector3I.Left,
            _ => position
        };
    }
    
    private static Direction GetOppositeDirection(Direction direction)
    {
        return direction switch
        {
            Direction.North => Direction.South,
            Direction.East => Direction.West,
            Direction.South => Direction.North,
            Direction.West => Direction.East,
            _ => direction
        };
    }
}