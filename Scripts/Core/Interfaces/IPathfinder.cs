using System.Collections.Generic;

namespace CardCleaner.Scripts.Core.Interfaces;

/// <summary>Finds a cheapest cell path across a map.</summary>
public interface IPathfinder
{
    List<int> FindPath(int startCell, int goalCell);
}
