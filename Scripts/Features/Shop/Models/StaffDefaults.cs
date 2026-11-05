namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>The staff roles the shop ships with, and the constants they are built from.</summary>
public static class StaffDefaults
{
    public const string CashierId = "cashier";
    public const string CashierUnlockId = "staff_cashier";
    public const string CashierName = "Cashier";
    public const int CashierHireFee = 200;
    public const int CashierDailyWage = 40;
    public const float CashierSecondsPerScan = 1.5f;
    public const float CashierSecondsPerPayment = 2.0f;
    public const float CashierTakeOverDelay = 3.0f;

    public static StaffRole Cashier() => new(
        CashierId,
        CashierName,
        CashierUnlockId,
        CashierHireFee,
        CashierDailyWage,
        CashierSecondsPerScan,
        CashierSecondsPerPayment,
        CashierTakeOverDelay);
}
