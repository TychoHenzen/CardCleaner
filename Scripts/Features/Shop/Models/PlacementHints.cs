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

    /// <summary>
    ///     The keys while the player aims at a placed piece: G moves it and B packs it into its box. A shelf that holds
    ///     stock can be neither, so it gets one hint that says so.
    /// </summary>
    public static IReadOnlyList<ControlHint> Aimed(string name, bool canMove)
    {
        var piece = name.ToLowerInvariant();
        if (!canMove)
            return [new ControlHint("G", $"Empty this {piece} to move or pack it")];
        return [new ControlHint("G", $"Move this {piece}"), new ControlHint("B", $"Pack this {piece} into its box")];
    }
}
