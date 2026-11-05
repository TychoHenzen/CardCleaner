namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>What the cashier does on an <see cref="CashierPlan.Advance"/> call.</summary>
public enum CashierAction
{
    None,
    Scan,
    Pay,
}
