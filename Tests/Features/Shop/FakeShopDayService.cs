using System;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;

namespace CardCleaner.Tests.Features.Shop;

/// <summary>
///     A shop day with a settable open state and no clock, debt or settlement. <see cref="OpenShop" /> and
///     <see cref="CloseShop" /> flip <see cref="IsOpen" /> and raise <see cref="Opened" />; the other events are
///     never raised here.
/// </summary>
public sealed class FakeShopDayService : IShopDayService
{
    public bool IsOpen { get; set; }

    public int Day => 1;

    public int Debt => 0;

    public int Takings => 0;

    public int MinutesOfDay => 0;

    public int OpeningMinute => 0;

    public int ClosingMinute => 0;

    private Action? _opened;

    public event Action Opened
    {
        add => _opened += value;
        remove => _opened -= value;
    }

    public event Action<DayReport> Closed
    {
        add { }
        remove { }
    }

    public event Action Restored
    {
        add { }
        remove { }
    }

    public event Action<int, int> TimeChanged
    {
        add { }
        remove { }
    }

    public void OpenShop()
    {
        if (IsOpen)
            return;

        IsOpen = true;
        _opened?.Invoke();
    }

    public void CloseShop() => IsOpen = false;
}
