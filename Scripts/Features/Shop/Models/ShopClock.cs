using System;

namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>
///     The shop's day clock. Time only moves while the shop is open; the day runs from open to close over
///     <see cref="DayLengthSeconds" /> of simulated time, and <see cref="MinutesOfDay" /> maps that onto the clock face.
/// </summary>
public sealed class ShopClock
{
    private readonly float _openMinutes;
    private readonly float _closeMinutes;

    /// <summary>Simulated seconds from opening to closing time.</summary>
    public float DayLengthSeconds { get; }

    /// <summary>The current day, starting at 1.</summary>
    public int Day { get; private set; } = 1;

    /// <summary>Whether the shop is open and time is moving.</summary>
    public bool IsOpen { get; private set; }

    /// <summary>Seconds elapsed since the current day opened, clamped to <see cref="DayLengthSeconds" />.</summary>
    public float SecondsIntoDay { get; private set; }

    /// <summary>Minute of the clock face: open minutes at the start of the day, close minutes at the end.</summary>
    public float MinutesOfDay =>
        _openMinutes + (_closeMinutes - _openMinutes) * SecondsIntoDay / DayLengthSeconds;

    public ShopClock(float openMinutes = 480, float closeMinutes = 1080, float dayLengthSeconds = 600)
    {
        if (!(dayLengthSeconds > 0))
            throw new ArgumentOutOfRangeException(nameof(dayLengthSeconds), dayLengthSeconds,
                "The day length must be positive.");
        if (!(closeMinutes > openMinutes))
            throw new ArgumentOutOfRangeException(nameof(closeMinutes), closeMinutes,
                "The close must be after the open.");

        _openMinutes = openMinutes;
        _closeMinutes = closeMinutes;
        DayLengthSeconds = dayLengthSeconds;
    }

    /// <summary>Opens the shop. Does nothing when it is already open.</summary>
    public void Open()
    {
        IsOpen = true;
    }

    /// <summary>
    ///     Closes the shop. Returns true only when this call moved the shop from open to closed.
    /// </summary>
    public bool Close()
    {
        if (!IsOpen)
            return false;

        IsOpen = false;
        return true;
    }

    /// <summary>
    ///     Moves time forward while open, clamped at the day length. Returns true when this call is the one that
    ///     reached closing time. ASSUMPTION: reaching closing time does not close the shop by itself; the caller
    ///     closes it with <see cref="Close" />, so the transition is reported once by that call.
    /// </summary>
    public bool Advance(float seconds)
    {
        if (seconds < 0)
            throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "Time cannot move backwards.");
        if (!IsOpen)
            return false;

        var wasAtClosing = SecondsIntoDay >= DayLengthSeconds;
        SecondsIntoDay = Math.Min(SecondsIntoDay + seconds, DayLengthSeconds);
        return !wasAtClosing && SecondsIntoDay >= DayLengthSeconds;
    }

    /// <summary>Moves to the next day: the shop is closed and the day clock is back at opening time.</summary>
    public void StartNextDay()
    {
        Day++;
        SecondsIntoDay = 0;
        IsOpen = false;
    }

    /// <summary>Restores a saved day. The shop is closed and the clock is back at opening time.</summary>
    public void Restore(int day)
    {
        Day = Math.Max(1, day);
        SecondsIntoDay = 0;
        IsOpen = false;
    }
}
