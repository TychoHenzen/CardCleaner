namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>One item in a checkout basket: its id and the price on its tag.</summary>
public readonly record struct CheckoutItem(int Id, int AskingPrice);
