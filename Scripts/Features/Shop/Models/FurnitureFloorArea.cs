using System.Collections.Generic;
using System.Linq;

namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>
///     The floor furniture may stand on: one rectangle per room. A piece fits when a single room holds its whole
///     footprint, so no piece straddles the wall between two rooms. Pure data.
/// </summary>
public sealed record FurnitureFloorArea(IReadOnlyList<FurnitureRect> Rooms)
{
    /// <summary>True when one of the rooms holds <paramref name="rect" /> on all four sides.</summary>
    public bool Holds(FurnitureRect rect) => Rooms.Any(room => room.Contains(rect));
}
