using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>The result of asking to buy one expansion lot.</summary>
public enum PurchaseOutcome
{
    Bought,
    OutOfOrder,
    Locked,
    Unaffordable,
    AlreadyOwned,
    Unknown,
}

/// <summary>
///     The expansion lots in purchase order and which of them are owned. Immutable: <see cref="With" /> returns a new
///     plan. Pure rules, no engine types.
/// </summary>
public sealed record ExpansionPlan
{
    private readonly IReadOnlySet<string> _owned;

    /// <summary>
    ///     Builds the plan. Throws <see cref="ArgumentException" /> when two lots share an order, which would make the
    ///     purchase order ambiguous.
    /// </summary>
    public ExpansionPlan(IReadOnlyList<ExpansionLot> lots, IReadOnlySet<string> owned)
    {
        ArgumentNullException.ThrowIfNull(lots);
        ArgumentNullException.ThrowIfNull(owned);

        var sorted = lots.OrderBy(lot => lot.Order).ToList();
        for (var i = 1; i < sorted.Count; i++)
        {
            if (sorted[i].Order == sorted[i - 1].Order)
                throw new ArgumentException($"Two expansion lots share order {sorted[i].Order}.", nameof(lots));
        }

        // ASSUMPTION (Low): a repeated lot id would make Decide ambiguous, so it is refused like a repeated order.
        var duplicateId = sorted.GroupBy(lot => lot.Id, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);
        if (duplicateId != null)
            throw new ArgumentException($"Two expansion lots share id '{duplicateId.Key}'.", nameof(lots));

        Lots = sorted;
        _owned = new HashSet<string>(owned, StringComparer.Ordinal);
    }

    /// <summary>The lots in ascending purchase order.</summary>
    public IReadOnlyList<ExpansionLot> Lots { get; }

    /// <summary>The ids of the lots already owned.</summary>
    public IReadOnlySet<string> Owned => _owned;

    /// <summary>The first lot in purchase order that is not owned, or null when every lot is owned.</summary>
    public ExpansionLot? Next => Lots.FirstOrDefault(lot => !_owned.Contains(lot.Id));

    /// <summary>
    ///     Decides a purchase without changing the plan. Checks run in this order: unknown id, already owned, out of
    ///     order, locked, unaffordable.
    /// </summary>
    public PurchaseOutcome Decide(string lotId, bool unlocked, int balance)
    {
        var lot = Lots.FirstOrDefault(candidate => candidate.Id == lotId);
        if (lot == null)
            return PurchaseOutcome.Unknown;
        if (_owned.Contains(lotId))
            return PurchaseOutcome.AlreadyOwned;
        if (Next is { } next && next.Id != lotId)
            return PurchaseOutcome.OutOfOrder;
        if (!unlocked)
            return PurchaseOutcome.Locked;
        if (balance < lot.Price)
            return PurchaseOutcome.Unaffordable;
        return PurchaseOutcome.Bought;
    }

    /// <summary>A new plan with <paramref name="lotId" /> added to the owned lots. This plan is unchanged.</summary>
    public ExpansionPlan With(string lotId)
    {
        var owned = new HashSet<string>(_owned, StringComparer.Ordinal) { lotId };
        return new ExpansionPlan(Lots, owned);
    }
}
