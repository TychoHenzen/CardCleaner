using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;

namespace CardCleaner.Scripts.Features.Packs.Models;

/// <summary>The key hints while the player carries a container.</summary>
public static class ContainerHints
{
    /// <summary>
    ///     The right button opens or closes a box or pack (<paramref name="lidOpen" /> says which it is now) and opens
    ///     a booster; Q puts the container down. An open cardboard box also names the left button: with
    ///     <paramref name="itemsInside" /> above zero it sets one item down on the floor, and an empty one goes in a
    ///     trash can.
    /// </summary>
    public static IReadOnlyList<ControlHint> For(CardContainerKind kind, bool lidOpen, int itemsInside = 0)
    {
        var hints = new List<ControlHint>
        {
            new("RMB", $"{(lidOpen ? "Close" : "Open")} the {CardContainerKindText.NameOf(kind)}"),
            new("Q", "Put it down"),
        };
        if (kind == CardContainerKind.Box && lidOpen)
            hints.Add(new ControlHint("LMB", itemsInside > 0 ? "Put one on the floor" : "Throw it in a trash can"));
        return hints;
    }
}
