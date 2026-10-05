namespace CardCleaner.Scripts.Features.Shop.Services;

public enum SaleStatus
{
    Sold,
    NothingToSell,
    NotSellable,
    AlreadySold
}

/// <summary>Outcome of a sale. <see cref="Price" /> and <see cref="Balance" /> are only set when sold.</summary>
public readonly record struct SaleResult(SaleStatus Status, int Price = 0, int Balance = 0)
{
    public bool Succeeded => Status == SaleStatus.Sold;
}
