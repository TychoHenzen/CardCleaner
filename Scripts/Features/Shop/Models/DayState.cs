using System;

namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>
///     A saved shop day: the day number and the debt carried into it. The save reads its numbers back as long, so
///     <see cref="TryParse" /> takes long values and checks them against the int range before it builds a state.
/// </summary>
public sealed record DayState(int Day, int Debt)
{
    /// <summary>
    ///     Builds a state from the saved day and debt. Returns false, with day 1 and no debt, when either value is
    ///     missing or outside the int range. Otherwise the day is clamped to at least 1 and the debt to at least 0.
    /// </summary>
    public static bool TryParse(long? day, long? debt, out DayState state)
    {
        if (day is null || debt is null || !InIntRange(day.Value) || !InIntRange(debt.Value))
        {
            state = new DayState(1, 0);
            return false;
        }

        state = new DayState(Math.Max(1, (int)day.Value), Math.Max(0, (int)debt.Value));
        return true;
    }

    private static bool InIntRange(long value) => value >= int.MinValue && value <= int.MaxValue;
}
