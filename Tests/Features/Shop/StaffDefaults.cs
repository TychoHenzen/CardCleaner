using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Tests.Features.Shop;

/// <summary>
///     The cashier values that <c>Data/Shop/StaffCatalog.tres</c> ships, written out for tests. The catalog is the
///     only source in the game; <c>StaffCatalogTest</c> fails when the shipped catalog and these values differ.
/// </summary>
public static class StaffDefaults
{
    public const string CashierId = StaffRoleIds.Cashier;
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
