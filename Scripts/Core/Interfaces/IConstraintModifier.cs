using System.Collections.Generic;
using CardCleaner.Scripts.Core.Enum;

namespace CardCleaner.Scripts.Features.Worldgen;

/// <summary>
/// Interface for tiles that can modify constraints on other layers
/// </summary>
public interface IConstraintModifier
{
    /// <summary>
    /// Get constraint modifications this tile applies to other layers
    /// </summary>
    Dictionary<(TileLayer layer, Direction direction), SocketType> GetConstraintModifications();
}