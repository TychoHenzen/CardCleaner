namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>
/// One kind of staff the shop can hire. The shop level that unlocks it lives in <c>UnlockCatalog</c>,
/// keyed by <see cref="UnlockId"/>, not on the role.
/// </summary>
public sealed record StaffRole(
    string Id,
    string DisplayName,
    string UnlockId,
    int HireFee,
    int DailyWage,
    float SecondsPerScan,
    float SecondsPerPayment,
    float TakeOverDelay);
