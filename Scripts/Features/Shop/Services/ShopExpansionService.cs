using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Services;

/// <summary>
///     Sells the storefront expansion lots. The lots come from the exported <see cref="Catalog" /> and are bought in
///     purchase order; the owned set lives in an immutable <see cref="ExpansionPlan" />. A lot is refused as Locked
///     when its unlock is not reached, or when no progression service is present (fails closed). Money is charged
///     only after the plan accepts the purchase, so a refused purchase charges nothing.
/// </summary>
[Service(typeof(IShopExpansion))]
public partial class ShopExpansionService : Node, IShopExpansion, IDependent
{
    private IServiceResolver? _services;
    private ExpansionPlan? _plan;

    // ASSUMPTION (Medium): the catalog is converted once to ExpansionLot records on the first read of Plan.
    // Assign it before the first purchase; a later change is not seen.
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

    /// <summary>The lots in purchase order and which of them are owned, as of now.</summary>
    public ExpansionPlan Plan => _plan ??= new ExpansionPlan(
        (Catalog?.Lots ?? []).Select(ToLot).ToList(),
        new HashSet<string>(StringComparer.Ordinal));

    /// <summary>The rent per day of every owned lot.</summary>
    public int DailyRent => ExpansionEffects.DailyRent(Plan);

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
        var unlocked = lot is not null && Progression is { } progression && progression.IsUnlocked(lot.UnlockId);
        var balance = Money?.Balance ?? 0;

        var outcome = plan.Decide(lotId, unlocked, balance);
        if (outcome != PurchaseOutcome.Bought || lot is null)
            return outcome;
        if (Money is null || !Money.TrySpend(lot.Price))
            return PurchaseOutcome.Unaffordable;

        _plan = plan.With(lotId);
        Opened?.Invoke(lotId);
        return PurchaseOutcome.Bought;
    }

    private static ExpansionLot ToLot(ExpansionLotData data) =>
        new(data.Id, data.Order, data.Price, data.UnlockId, data.DailyRent, data.ExtraCustomers, data.DisplayName,
            data.StorefrontMinX);
}
