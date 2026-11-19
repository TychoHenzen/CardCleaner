using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;

namespace CardCleaner.Scripts.Features.Packs.Models;

/// <summary>The key hints while the player carries a container.</summary>
public static class ContainerHints
{
    /// <summary>
    ///     The right button opens or closes a cardboard box (<paramref name="lidOpen" /> says which it is now), empties a
    ///     booster box into the hand and opens a booster pack; Q puts the container down. An open cardboard box also
    ///     names the left button: with <paramref name="itemsInside" /> above zero it sets one item down on the floor, and
    ///     an empty one goes in a trash can. With more than one booster pack <paramref name="held" />, the right button
    ///     opens one of them and Q puts one down.
    /// </summary>
    public static IReadOnlyList<ControlHint> For(CardContainerKind kind, bool lidOpen, int itemsInside = 0,
        int held = 1)
    {
        var name = CardContainerKindText.NameOf(kind);
        var hints = new List<ControlHint>
        {
            new("RMB", kind switch
            {
                CardContainerKind.Box => $"{(lidOpen ? "Close" : "Open")} the {name}",
                CardContainerKind.Pack => $"Empty the {name} into your hand",
                _ when held > 1 => $"Open one of the {held} {name}s",
                _ => $"Open the {name}",
            }),
            new("Q", held > 1 ? "Put one down" : "Put it down"),
        };
        if (kind == CardContainerKind.Box && lidOpen)
            hints.Add(new ControlHint("LMB", itemsInside > 0 ? "Put one on the floor" : "Throw it in a trash can"));
        return hints;
    }
}
