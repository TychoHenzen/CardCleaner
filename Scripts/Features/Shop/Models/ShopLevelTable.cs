using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>
///     The shop's level curve: the cumulative XP each level starts at. Level 1 starts at XP 0, and every later
///     threshold is the XP total at which that level begins. Pure rules, no engine types.
/// </summary>
public sealed class ShopLevelTable
{
    private readonly int[] _thresholds;

    /// <summary>
    ///     Builds a table from the cumulative XP thresholds, one per level. Throws <see cref="ArgumentException" />
    ///     when the list is empty, does not start at 0, or is not strictly increasing.
    /// </summary>
    public ShopLevelTable(IReadOnlyList<int> thresholds)
    {
        ArgumentNullException.ThrowIfNull(thresholds);
        if (thresholds.Count == 0)
            throw new ArgumentException("A level table needs at least one threshold.", nameof(thresholds));
        if (thresholds[0] != 0)
            throw new ArgumentException("The first level must start at XP 0.", nameof(thresholds));
        for (var i = 1; i < thresholds.Count; i++)
        {
            if (thresholds[i] <= thresholds[i - 1])
                throw new ArgumentException("Level thresholds must be strictly increasing.", nameof(thresholds));
        }

        _thresholds = thresholds.ToArray();
    }

    /// <summary>The curve the shop ships with.</summary>
    public static ShopLevelTable Default => new ShopLevelTable(new[] { 0, 50, 150, 300, 500 });

    /// <summary>The highest level the table reaches.</summary>
    public int MaxLevel => _thresholds.Length;

    /// <summary>The level that the given XP total reaches. Negative XP counts as 0, so it is level 1.</summary>
    public int LevelFor(int xp)
    {
        var index = 0;
        while (index + 1 < _thresholds.Length && _thresholds[index + 1] <= xp)
            index++;
        return index + 1;
    }

    /// <summary>XP gained inside the current level. Zero at the top level.</summary>
    public int XpIntoLevel(int xp)
    {
        var index = LevelFor(xp) - 1;
        if (index == _thresholds.Length - 1)
            return 0;
        return Math.Max(0, xp - _thresholds[index]);
    }

    /// <summary>The span of the current level in XP, or null at the top level.</summary>
    public int? XpForNextLevel(int xp)
    {
        var index = LevelFor(xp) - 1;
        if (index == _thresholds.Length - 1)
            return null;
        return _thresholds[index + 1] - _thresholds[index];
    }

    /// <summary>Fraction of the current level already earned, from 0 to 1. The top level reads as 1.</summary>
    public float Progress(int xp)
    {
        var span = XpForNextLevel(xp);
        if (span is null)
            return 1.0f;
        return (float)XpIntoLevel(xp) / span.Value;
    }
}
