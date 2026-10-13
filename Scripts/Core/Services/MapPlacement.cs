using System.Collections.Generic;

namespace CardCleaner.Scripts.Core.Services;

/// <summary>
/// Where <see cref="MapPopulation"/> put the player and the enemies, as cell ids of the map it was given.
/// </summary>
public sealed record MapPlacement(int PlayerStartCell, IReadOnlyList<int> EnemyCells);
