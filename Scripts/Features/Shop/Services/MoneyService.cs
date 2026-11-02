using System;
using CardCleaner.Scripts.Core.DependencyInjection;
using Godot;
using Saveable;

namespace CardCleaner.Scripts.Features.Shop.Services;

/// <summary>
///     Minimal money balance for the shop. Place it in a scene under an AutoServiceProvider.
/// </summary>
[Service(typeof(IMoneyService))]
public partial class MoneyService : Node, IMoneyService, ISaveable
{
    private const int DefaultStartingBalance = 500;
    private const string BalanceKey = "balance";

    private bool _initialized;
    private int _balance;

    [Export]
    public int StartingBalance { get; set; } = DefaultStartingBalance;

    public StringName UniqueID => "money";

    public void Save(NodeSave save)
    {
        EnsureInitialized();
        save.SetOrAddProperty(BalanceKey, _balance);
    }

    // JSON numbers come back as long, so the balance is read as long and clamped into the int range.
    public void Load(NodeSave save)
    {
        if (!save.TryGetProperty<long>(BalanceKey, out var saved))
            return;

        _initialized = true;
        _balance = (int)Math.Clamp(saved, 0, int.MaxValue);
        BalanceChanged?.Invoke(_balance);
    }

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
