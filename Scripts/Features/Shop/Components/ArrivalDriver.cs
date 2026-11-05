using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     Follows the shop day for a <see cref="CustomerSpawner" />: builds the day's arrival schedule when the shop
///     opens, calls <c>spawn</c> for each arrival that has come due while it is open, and drops the schedule when
///     the day closes or a save is restored. It names no Godot type.
/// </summary>
internal sealed class ArrivalDriver
{
    private readonly Func<IRandom> _random;
    private readonly Func<int> _customersPerDay;
    private readonly Action _spawn;
    private IReadOnlyList<int> _schedule = Array.Empty<int>();
    private IShopDayService? _day;
    private int _next;

    /// <param name="random">The random source for the arrival minutes.</param>
    /// <param name="customersPerDay">How many customers today's schedule holds.</param>
    /// <param name="spawn">Spawns one customer.</param>
    public ArrivalDriver(Func<IRandom> random, Func<int> customersPerDay, Action spawn)
    {
        _random = random;
        _customersPerDay = customersPerDay;
        _spawn = spawn;
    }

    /// <summary>Raised when the shop opens, before the schedule is built.</summary>
    public event Action? Opened;

    /// <summary>Scheduled arrivals that have not happened yet.</summary>
    public int Pending => _schedule.Count - _next;

    /// <summary>Follows <paramref name="day" /> instead of the day followed so far; null follows none.</summary>
    public void Follow(IShopDayService? day)
    {
        Release();
        _day = day;
        Attach();
    }

    /// <summary>Subscribes to the followed day again. Safe to call while already subscribed.</summary>
    public void Attach()
    {
        Release();
        if (_day is not { } day)
            return;

        day.Opened += OnOpened;
        day.Closed += OnClosed;
        day.Restored += Clear;
        day.TimeChanged += OnTimeChanged;
    }

    /// <summary>Drops every subscription; the followed day stays remembered.</summary>
    public void Release()
    {
        if (_day is not { } day)
            return;

        day.Opened -= OnOpened;
        day.Closed -= OnClosed;
        day.Restored -= Clear;
        day.TimeChanged -= OnTimeChanged;
    }

    private void OnOpened()
    {
        Opened?.Invoke();
        Clear();
        var day = _day!;
        if (day.ClosingMinute - day.OpeningMinute <= ArrivalSchedule.LastEntryMarginMinutes)
        {
            ILog.Error("The shop is open too short a time for any arrival.");
            return;
        }

        _schedule = ArrivalSchedule.Build(day.OpeningMinute, day.ClosingMinute, _customersPerDay(), _random());
    }

    private void OnTimeChanged(int day, int minute)
    {
        if (_day is not { IsOpen: true })
            return;

        while (_next < _schedule.Count && _schedule[_next] <= minute)
        {
            _next++;
            _spawn();
        }
    }

    // A close ends the day's arrivals: the rest of the schedule counts as done.
    private void OnClosed(DayReport report) => _next = _schedule.Count;

    // A restore and a new opening each start from no schedule.

    private void Clear()
    {
        _schedule = Array.Empty<int>();
        _next = 0;
    }
}
