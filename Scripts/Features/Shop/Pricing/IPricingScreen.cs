
namespace CardCleaner.Scripts.Features.Shop.Pricing;

/// <summary>The screen where the player sets the price of the stock a price tag shows.</summary>
public interface IPricingScreen
{
    bool IsOpen { get; }

    /// <summary>Shows the stock's figures with its current price, ready to change.</summary>
    void Open(IPricedStock stock);
}
