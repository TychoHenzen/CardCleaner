using System;

namespace CardCleaner.Scripts.Features.Shop.Services;

/// <summary>
///     The shop's XP and level. Checkouts add XP; an item's unlock level decides whether it can be ordered.
/// </summary>
public interface IShopProgression
{
    int Xp { get; }

    int Level { get; }

    /// <summary>Fraction of the current level already earned, from 0 to 1.</summary>
    float Progress { get; }

    bool AtMaxLevel { get; }

    /// <summary>Raised after every change to the XP total, including a load.</summary>
    event Action? ProgressChanged;

    /// <summary>
    ///     Adds the XP a checkout of <paramref name="amountPaid" /> earns.
    ///     A non-positive amount earns nothing.
    /// </summary>
    void AwardCheckout(int amountPaid);

    /// <summary>True when the id has an unlock row and the shop is at or above that row's level.</summary>
    bool IsUnlocked(string id);

    /// <summary>The level that unlocks the id, or null when the id has no unlock row.</summary>
    int? RequiredLevel(string id);
}
