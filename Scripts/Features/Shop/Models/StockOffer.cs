using CardCleaner.Scripts.Features.Packs.Models;

namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>
///     What one stock click would put on a shelf: one <see cref="Stock" />. When it comes out of an open box or pack,
///     <see cref="Left" /> is how many of that stock are still inside that container, this one included; it is null for
///     a card from the hand or a whole container.
/// </summary>
public readonly record struct StockOffer(StockKind Stock, int? Left = null)
{
    /// <summary>
    ///     The click prompt: "Put the card here", "Put the booster box here", or "Put a booster box here (8 in the
    ///     box)" for one taken out of an open box.
    /// </summary>
    public string Prompt => Stock switch
    {
        StockKind.Card when Left is { } cards => $"Put a card here ({cards} in the box)",
        StockKind.Card => "Put the card here",
        _ when Left is { } left => $"Put a {NameOf(Stock)} here ({left} in the box)",
        _ => $"Put the {NameOf(Stock)} here",
    };

    private static string NameOf(StockKind stock) => CardContainerKindText.NameOf(stock switch
    {
        StockKind.Box => CardContainerKind.Box,
        StockKind.Pack => CardContainerKind.Pack,
        _ => CardContainerKind.Booster,
    });
}
