using System;
using CardCleaner.Scripts.Features.Packs.Models;

namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>The stable id of each kind of sealed product.</summary>
public static class ProductIds
{
    /// <summary>
    ///     The id of the kind: its item id in the order catalog (<c>BackofficeCatalog.tres</c>). Never the name the
    ///     player reads (<see cref="CardContainerKindText.NameOf" />), which may change.
    /// </summary>
    public static string IdOf(CardContainerKind kind) => kind switch
    {
        CardContainerKind.Box => "cardboard_box",
        CardContainerKind.Pack => "pack",
        CardContainerKind.Booster => "booster",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown container kind")
    };
}
