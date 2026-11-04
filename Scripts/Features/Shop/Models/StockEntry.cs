namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>
///     One item on the Stock app: its id, name, price and the shop level that unlocks it.
///     A null <see cref="RequiredLevel"/> means the id has no unlock row, so the item stays locked at every level.
///     Pure values, no engine types.
/// </summary>
public readonly record struct StockEntry(string Id, string Name, int Price, int? RequiredLevel)
{
    public bool IsLocked(int shopLevel) => RequiredLevel is not { } required || required > shopLevel;
}
