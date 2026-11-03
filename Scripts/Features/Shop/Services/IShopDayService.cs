using System;
using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Scripts.Features.Shop.Services;

/// <summary>
///     The shop's opening hours: opens and closes the day, moves the day clock, and settles the bills at close.
/// </summary>
public interface IShopDayService
{
    /// <summary>Whether the shop is open and time is moving.</summary>
    bool IsOpen { get; }

    /// <summary>The current day, starting at 1.</summary>
    int Day { get; }

    /// <summary>The debt carried into the next day.</summary>
    int Debt { get; }

    /// <summary>The current day's takings, 0 when the shop is closed.</summary>
    int Takings { get; }

    /// <summary>Whole minutes of the clock face.</summary>
    int MinutesOfDay { get; }

    /// <summary>Raised when the shop opens.</summary>
    event Action Opened;

    /// <summary>Raised with the day's report when the shop closes.</summary>
    event Action<DayReport> Closed;

    /// <summary>Raised when a saved day is restored.</summary>
    event Action Restored;

    /// <summary>Raised with (day, minutesOfDay) when the clock face moves to a new minute.</summary>
    event Action<int, int> TimeChanged;

    /// <summary>Opens the shop. Does nothing when it is already open.</summary>
    void OpenShop();

    /// <summary>
    ///     Closes the shop, charges the day's bills and starts the next day. Does nothing when it is closed.
    /// </summary>
    void CloseShop();
}
