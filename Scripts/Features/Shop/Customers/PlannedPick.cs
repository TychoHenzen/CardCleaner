namespace CardCleaner.Scripts.Features.Shop.Customers;

/// <summary>One offer a customer will try, with the outcome of its buy decision already rolled.</summary>
public readonly record struct PlannedPick(ShelfOffer Offer, BuyOutcome Outcome);
