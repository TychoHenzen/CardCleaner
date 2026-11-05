using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     Sends a fresh customer shopping: plans the visit against the shelf offers no other customer has claimed,
///     claims the offers it buys, picks the payment method, and starts the visit. The claims are released when the
///     customer finishes.
/// </summary>
internal sealed class CustomerBriefing
{
    private readonly HashSet<string> _claimed = new(StringComparer.Ordinal);
    private readonly Func<SceneTree> _tree;
    private readonly Func<IRandom> _random;

    /// <param name="tree">The tree whose shelves the customers shop at.</param>
    /// <param name="random">The random source for wishes, rolls and the payment method.</param>
    public CustomerBriefing(Func<SceneTree> tree, Func<IRandom> random) => (_tree, _random) = (tree, random);

    /// <summary>Offers currently claimed by customers on their way to buy them.</summary>
    public int ClaimedCount => _claimed.Count;

    public void Send(Customer customer, Vector3 counter, Vector3 exit)
    {
        var rng = _random();
        var offers = new ShelfOfferIndex(_tree());
        var plan = ShoppingPlanner.Plan(offers.Collect(), _claimed, ShoppingPlanner.DrawWish(rng), rng);
        // ASSUMPTION (Low): about half of the customers pay by card; the draw comes after the plan draws so the plan
        // of each customer is unchanged.
        customer.Method = rng.RandiRange(0, 1) == 0 ? PaymentMethod.Card : PaymentMethod.Cash;
        Claim(customer, plan);
        customer.Begin(plan, offers, counter, exit);
    }

    private void Claim(Customer customer, IReadOnlyList<PlannedPick> plan)
    {
        var claims = plan.Where(pick => pick.Outcome.Buys).Select(pick => pick.Offer.OfferId).ToList();
        _claimed.UnionWith(claims);
        customer.Finished += _ => _claimed.ExceptWith(claims);
    }
}
