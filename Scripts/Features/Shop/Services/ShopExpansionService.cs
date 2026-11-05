using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;
using Saveable;

namespace CardCleaner.Scripts.Features.Shop.Services;

/// <summary>
///     Sells the storefront expansion lots. The lots come from the exported <see cref="Catalog" /> and are bought in
///     purchase order; the owned set lives in an immutable <see cref="ExpansionPlan" />. A lot is refused as Locked
///     when its unlock is not reached, or when no progression service is present (fails closed). Money is charged
///     only after the plan accepts the purchase, so a refused purchase charges nothing.
/// </summary>
[Service(typeof(IShopExpansion))]
public partial class ShopExpansionService : Node, IShopExpansion, IDependent, ILoadsFirst
{
    private const string OwnedKey = "owned";

    private IServiceResolver? _services;
    private ExpansionPlan? _plan;

    /// <summary>
    ///     The purchasable lots, in any order. The purchase order is each lot's
    ///     <see cref="ExpansionLotData.Order" />.
    /// </summary>
    [Export]
    public ExpansionLotCatalog? Catalog { get; set; }

    /// <summary>Resolved through the injected service resolver at startup; assignable directly in tests.</summary>
    public IMoneyService? Money { get; set; }

    /// <summary>Resolved through the injected service resolver at startup; assignable directly in tests.</summary>
    public IShopProgression? Progression { get; set; }

    /// <summary>Raised once for each lot bought, with the lot's id.</summary>
    public event Action<string>? Opened;

    public StringName UniqueID => "expansions";

    /// <summary>The lots in purchase order and which of them are owned, as of now.</summary>
    // ASSUMPTION (Medium): the catalog is converted once to ExpansionLot records on the first read of Plan.
    // Assign Catalog before the first purchase; a later change is not seen.
    public ExpansionPlan Plan => _plan ??= new ExpansionPlan(
        (Catalog?.Lots ?? []).Select(ToLot).ToList(),
        new HashSet<string>(StringComparer.Ordinal));

    /// <summary>The extra customers per day that every owned lot adds.</summary>
    public int ExtraCustomersPerDay => ExpansionEffects.ExtraCustomers(Plan);

    public void Inject(IServiceResolver services) => _services = services;

    public override void _Ready()
    {
        if (_services == null)
        {
            ILog.Error($"{Name} was not injected");
            return;
        }

        _services.Get<IMoneyService>(money => Money ??= money);
        _services.Get<IShopProgression>(progression => Progression ??= progression);
    }

    /// <summary>
    ///     Buys a lot when the plan accepts it, the progression has its unlock and the balance covers its price.
    ///     A refused purchase charges nothing and raises nothing.
    /// </summary>
    public PurchaseOutcome TryBuy(string lotId)
    {
        var plan = Plan;
        var lot = plan.Lots.FirstOrDefault(candidate => candidate.Id == lotId);
        if (lot is null)
            return PurchaseOutcome.Unknown;

        var unlocked = Progression is { } progression && progression.IsUnlocked(lot.UnlockId);
        var outcome = plan.Decide(lotId, unlocked, Money?.Balance ?? 0);
        if (outcome != PurchaseOutcome.Bought)
            return outcome;
        if (Money is null || !Money.TrySpend(lot.Price))
            return PurchaseOutcome.Unaffordable;

        _plan = plan.With(lotId);
        Opened?.Invoke(lotId);
        return PurchaseOutcome.Bought;
    }

    /// <summary>Saves the ids of the owned lots as one JSON list.</summary>
    public void Save(NodeSave save) =>
        save.SetOrAddProperty(OwnedKey, JsonSerializer.Serialize(Plan.Owned.Order(StringComparer.Ordinal).ToList()));

    /// <summary>
    ///     Replaces the owned lots with the saved ids. An id that is not in the catalog is logged and ignored, and a
    ///     list that cannot be read leaves the plan as it is. Every lot now owned raises <see cref="Opened" />, so the
    ///     scene opens the lots the same way a purchase does.
    /// </summary>
    public void Load(NodeSave save)
    {
        if (!save.TryGetProperty<string>(OwnedKey, out var json) || json is null)
            return;

        List<string>? saved;
        try
        {
            saved = JsonSerializer.Deserialize<List<string>>(json);
        }
        catch (JsonException)
        {
            saved = null;
        }

        if (saved is null)
        {
            ILog.Warning("Expansion save is unreadable; the owned lots are unchanged");
            return;
        }

        var known = Plan.Lots.Select(lot => lot.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var id in saved.Where(id => !known.Contains(id)))
            ILog.Warning($"Expansion save names an unknown lot '{id}'; ignored");

        _plan = new ExpansionPlan(Plan.Lots, saved.Where(known.Contains).ToHashSet(StringComparer.Ordinal));
        foreach (var lot in _plan.Lots.Where(lot => _plan.Owned.Contains(lot.Id)))
            Opened?.Invoke(lot.Id);
    }

    private static ExpansionLot ToLot(ExpansionLotData data) =>
        new(data.Id, data.Order, data.Price, data.UnlockId, data.DailyRent, data.ExtraCustomers, data.DisplayName,
            data.StorefrontMinX);
}
