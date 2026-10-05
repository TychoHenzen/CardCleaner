using System;
using CardCleaner.Scripts.Core.DependencyInjection;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Services;

/// <summary>
///     Minimal money balance for the shop. Place it in a scene under an AutoServiceProvider.
/// </summary>
[Service(ServiceLifetime.Singleton, typeof(IMoneyService))]
public partial class MoneyService : Node, IMoneyService
{
    private const int DefaultStartingBalance = 500;

    private bool _initialized;
    private int _balance;

    [Export]
    public int StartingBalance { get; set; } = DefaultStartingBalance;

    public int Balance
    {
        get
        {
            EnsureInitialized();
            return _balance;
        }
    }

    public event Action<int>? BalanceChanged;

    public bool CanAfford(int amount) => amount >= 0 && Balance >= amount;

    public bool TrySpend(int amount)
    {
        if (amount <= 0 || !CanAfford(amount))
            return false;

        _balance -= amount;
        BalanceChanged?.Invoke(_balance);
        return true;
    }

    public void Add(int amount)
    {
        if (amount <= 0)
            return;

        EnsureInitialized();
        _balance += amount;
        BalanceChanged?.Invoke(_balance);
    }

    // The balance starts from the exported value the first time anyone reads or changes it.
    private void EnsureInitialized()
    {
        if (_initialized)
            return;
        _initialized = true;
        _balance = StartingBalance;
    }
}
