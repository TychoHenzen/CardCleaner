using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;

namespace CardCleaner.Scripts.Features.Packs.Models;

/// <summary>The key hints while the player carries a sealed container.</summary>
public static class ContainerHints
{
    /// <summary>
    ///     Q unpacks onto the shelf the player faces when <paramref name="atShelf" /> is true, else opens the container
    ///     where the player stands; the right button puts it down.
    /// </summary>
    public static IReadOnlyList<ControlHint> For(CardContainerKind kind, bool atShelf) =>
    [
        new("Q", atShelf
            ? $"Unpack the {CardContainerKindText.ContentsOf(kind)} onto this shelf"
            : $"Open the {CardContainerKindText.NameOf(kind)}"),
        new("RMB", "Put it down"),
    ];
}
