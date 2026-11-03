namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>
///     One stocked shelf slot a customer may buy: the slot's node path, its asking price and its market value.
/// </summary>
public readonly record struct ShelfOffer(string OfferId, int AskingPrice, int MarketValue);
