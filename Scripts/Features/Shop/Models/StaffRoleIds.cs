namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>
///     The ids of the staff roles that code looks up by name. Everything else about a role (fee, wage, timings)
///     comes from <c>Data/Shop/StaffCatalog.tres</c> and nowhere else.
/// </summary>
public static class StaffRoleIds
{
    public const string Cashier = "cashier";
}
