using System;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;
using Saveable;

namespace CardCleaner.Scripts.Features.Shop.Services;

/// <summary>
///     The shop's XP total, saved with the slot. Each checkout adds the XP its amount paid earns, and the level
///     curve and unlock rows in the pure models decide what that total opens. Place it in a scene under an
///     AutoServiceProvider.
/// </summary>
[Service(typeof(IShopProgression))]
public partial class ShopProgression : Node, IShopProgression, ISaveable
{
    private const string XpKey = "xp";

    // Keeps XP plus one checkout's gain inside int.MaxValue, so the total never overflows.
    private const int MaxXp = int.MaxValue / 2;

    private readonly ShopLevelTable _levels = ShopLevelTable.Default;
    private readonly UnlockCatalog _unlocks = UnlockCatalog.Default;
    private int _xp;

    public StringName UniqueID => "shop_progression";

    public int Xp => _xp;

    public int Level => _levels.LevelFor(_xp);

    public float Progress => _levels.Progress(_xp);

    public bool AtMaxLevel => Level == _levels.MaxLevel;

    public event Action? ProgressChanged;

    public void AwardCheckout(int amountPaid)
    {
        var gain = CheckoutXp.For(amountPaid);
        if (gain <= 0)
            return;

        _xp = (int)Math.Min((long)_xp + gain, MaxXp);
        ILog.Print($"Shop XP +{gain}, total {_xp}");
        ProgressChanged?.Invoke();
    }

    public int? RequiredLevel(string id) => _unlocks.RequiredLevel(id);

    public bool IsUnlocked(string id) => RequiredLevel(id) is { } required && Level >= required;

    public void Save(NodeSave save) => save.SetOrAddProperty(XpKey, _xp);

    public void Load(NodeSave save)
    {
        _xp = ReadXp(save);
        ProgressChanged?.Invoke();
    }

    // Read as object: a typed read of a corrupt value throws and would stop every later saveable's load.
    private static int ReadXp(NodeSave save)
    {
        if (!save.TryGetProperty<object>(XpKey, out var raw))
            return 0;

        long? whole = raw switch
        {
            long value => value,
            int value => value,
            _ => null,
        };
        if (whole is not { } number)
        {
            ILog.Warning("shop_progression: saved xp is not a whole number; XP set to 0.");
            return 0;
        }

        return (int)Math.Clamp(number, 0, MaxXp);
    }
}
