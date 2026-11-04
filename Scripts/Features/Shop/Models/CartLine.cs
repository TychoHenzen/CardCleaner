namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>One cart line: an item and how many of it the player has chosen.</summary>
public sealed record CartLine(StockEntry Entry, int Quantity);
