using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;

namespace CardCleaner.Scripts.Features.Packs.Models;

/// <summary>The key hints while the player carries a sealed container.</summary>
public static class ContainerHints
{
    /// <summary>
    ///     The right button opens or closes a box or pack (<paramref name="lidOpen" /> says which it is now) and opens
    ///     a booster; Q puts the container down.
    /// </summary>
    public static IReadOnlyList<ControlHint> For(CardContainerKind kind, bool lidOpen) =>
    [
        new("RMB", $"{(lidOpen ? "Close" : "Open")} the {CardContainerKindText.NameOf(kind)}"),
        new("Q", "Put it down"),
    ];
}
