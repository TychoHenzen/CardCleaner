using System.Collections.Generic;

namespace CardCleaner.Scripts.Core.Interfaces;

/// <summary>
/// Extended fog of war interface for passive visibility updates.
/// Implementations receive pre-computed visibility sets from external sources.
/// </summary>
public interface IPassiveFogOfWar : IFogOfWar
{
    /// <summary>Updates visibility from externally computed seen and visible sets.</summary>
    void UpdateVisibility(IReadOnlySet<int> seenCellIds, IReadOnlySet<int> visibleCellIds);
}
