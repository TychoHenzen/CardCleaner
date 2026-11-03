using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Features.Shop.Services;

namespace CardCleaner.Tests.Features.Shop;

/// <summary>
///     An always-unlocked progression for suites that do not test the level lock. Pass one id to lock it: that id
///     then needs level 2, and the shop is at level 5 with no way to change that.
/// </summary>
public sealed class OpenProgression : IShopProgression
{
    private readonly string? _lockedId;

    public OpenProgression(string? lockedId = null)
    {
        _lockedId = lockedId;
    }

    /// <summary>Every checkout amount passed to <see cref="AwardCheckout" />, in order.</summary>
    public List<int> Awards { get; } = new();

    public int Xp => 0;

    public int Level => 5;

    public float Progress => 1f;

    public bool AtMaxLevel => true;

    // Explicit empty accessors: this progression never changes, so nothing ever raises the event.
    public event Action? ProgressChanged
    {
        add { }
        remove { }
    }

    public void AwardCheckout(int amountPaid) => Awards.Add(amountPaid);

    public bool IsUnlocked(string id) => id != _lockedId;

    public int? RequiredLevel(string id) => id == _lockedId ? 2 : 1;
}
