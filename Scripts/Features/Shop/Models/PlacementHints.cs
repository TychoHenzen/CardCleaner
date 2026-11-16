using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;

namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>The key hints of furniture placement: while a piece is carried, or while one is aimed at.</summary>
public static class PlacementHints
{
    /// <summary>The keys while a piece is carried. E only places it when the ghost shows a valid pose.</summary>
    public static IReadOnlyList<ControlHint> Carrying(bool valid) =>
    [
        new("T", "Turn"),
        new("E", valid ? "Place here" : "Cannot place here"),
        new("C", "Cancel"),
    ];

    /// <summary>The key while the player aims at a placed piece; a shelf that holds stock cannot be moved.</summary>
    public static IReadOnlyList<ControlHint> Aimed(string name, bool canMove)
    {
        var piece = name.ToLowerInvariant();
        return [new ControlHint("G", canMove ? $"Move this {piece}" : $"Empty this {piece} to move it")];
    }
}
