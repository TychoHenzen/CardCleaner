using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Shop.Models;
using Saveable;

namespace CardCleaner.Scripts.Features.Shop.Services;

/// <summary>
///     How the shop day is written into and read back from a <see cref="NodeSave" />: the day number and the
///     carried debt. Reading never throws; a missing, corrupt or out-of-range value is logged and replaced by the
///     nearest valid <see cref="DayState" />.
/// </summary>
public static class DaySaveFormat
{
    private const string DayKey = "day";
    private const string DebtKey = "debt";

    public static void Write(NodeSave save, int day, int debt)
    {
        save.SetOrAddProperty(DayKey, day);
        save.SetOrAddProperty(DebtKey, debt);
    }

    /// <param name="save">The saved node.</param>
    /// <param name="owner">The saving node's name, used in log messages.</param>
    public static DayState Read(NodeSave save, string owner)
    {
        var day = ReadWhole(save, DayKey);
        var debt = ReadWhole(save, DebtKey);
        if (!DayState.TryParse(day, debt, out var state))
        {
            ILog.Error($"{owner}: saved '{DayKey}' ({Describe(day)}) or '{DebtKey}' ({Describe(debt)}) is missing or "
                + "not a whole number in range; the day is set to 1 with no debt.");
            return state;
        }

        if (state.Day != day || state.Debt != debt)
            ILog.Warning($"{owner}: saved day or debt was out of range and has been clamped to day {state.Day}, "
                + $"debt {state.Debt}.");
        return state;
    }

    // Read as object: a typed read of a corrupt value throws and would stop every later saveable's load.
    private static long? ReadWhole(NodeSave save, string key)
    {
        if (!save.TryGetProperty<object>(key, out var raw))
            return null;
        return raw switch
        {
            long whole => whole,
            int whole => whole,
            _ => (long?)null,
        };
    }

    private static string Describe(long? value) => value is { } number ? number.ToString() : "missing";
}
