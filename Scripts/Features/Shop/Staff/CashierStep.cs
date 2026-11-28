namespace CardCleaner.Scripts.Features.Shop.Staff;

/// <summary>Where a cashier is in serving one customer.</summary>
public enum CashierStep
{
    Idle,
    Claiming,
    Scanning,
    Paying,
    Done,
}
