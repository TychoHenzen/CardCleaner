using System;

namespace CardCleaner.Scripts.Features.Shop.Services;

/// <summary>
///     The player's money balance. Not persisted.
/// </summary>
public interface IMoneyService
{
    int Balance { get; }

    /// <summary>Raised with the new balance after every change.</summary>
    event Action<int> BalanceChanged;

    /// <summary>Deducts <paramref name="amount" /> only when it is positive and affordable.</summary>
    bool TrySpend(int amount);

    /// <summary>Adds a positive <paramref name="amount" /> (refunds, and later sales income).</summary>
    void Add(int amount);

    bool CanAfford(int amount);
}
