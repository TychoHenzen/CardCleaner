using System;
using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Scripts.Features.Shop.Services;

/// <summary>The shop's storefront expansion lots: which are owned and how buying one goes.</summary>
public interface IShopExpansion
{
    /// <summary>The lots in purchase order and which of them are owned, as of now.</summary>
    ExpansionPlan Plan { get; }

    /// <summary>Raised once for each lot bought, with the lot's id. A refused purchase raises nothing.</summary>
    event Action<string>? Opened;

    /// <summary>Tries to buy a lot. A refused purchase charges nothing and changes nothing.</summary>
    PurchaseOutcome TryBuy(string lotId);

    /// <summary>The extra customers per day that every owned lot adds.</summary>
    int ExtraCustomersPerDay { get; }
}
