using System;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Controllers;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Services;

/// <summary>
///     Sells one card: pays its price into the money service and removes the card from the world. The price is the
///     slot's asking price when one is given, otherwise the card's <see cref="StockMarketValue" />. Only a signed
///     card is sellable. Something that cannot be sold is left exactly where it is.
/// </summary>
public static class CardSale
{
    private const string SoldMeta = "sold";

    /// <summary>
    ///     Sells <paramref name="card" />. <paramref name="release" /> detaches the card from wherever it is
    ///     held (a shelf slot or the player's hand) and runs only once the sale is certain. It returns
    ///     false when the card could not be detached, which aborts the sale unpaid. <paramref name="askingPrice" />
    ///     is the price a shelf slot asks for the card; null pays the card's market value.
    /// </summary>
    public static SaleResult TrySell(RigidBody3D? card, IMoneyService money, Func<RigidBody3D, bool>? release = null,
        int? askingPrice = null)
    {
        var refusal = Appraise(card, askingPrice, out var price);
        if (refusal != null)
            return new SaleResult(refusal.Value);

        if (!TryRelease(card!, release))
            return new SaleResult(SaleStatus.NothingToSell);

        money.Add(price);
        card.QueueFree();

        ILog.Print($"Card sold for {price}, balance now {money.Balance}");
        return new SaleResult(SaleStatus.Sold, price, money.Balance);
    }

    // Returns why the card cannot be sold, or null with the price to pay.
    private static SaleStatus? Appraise(RigidBody3D? card, int? askingPrice, out int price)
    {
        price = 0;
        if (card == null || !GodotObject.IsInstanceValid(card))
            return SaleStatus.NothingToSell;

        if (card.IsQueuedForDeletion() || card.HasMeta(SoldMeta))
            return SaleStatus.AlreadySold;

        if (card is not CardController { HasSignature: true } cardController)
            return SaleStatus.NotSellable;

        price = askingPrice ?? StockMarketValue.ForCard(cardController);
        return price <= 0 ? SaleStatus.NotSellable : null;
    }

    // Marks the card sold first so a second request in the same frame cannot pay out again.
    private static bool TryRelease(RigidBody3D card, Func<RigidBody3D, bool>? release)
    {
        card.SetMeta(SoldMeta, true);
        try
        {
            if (release == null || release(card))
                return true;

            card.RemoveMeta(SoldMeta);
            return false;
        }
        catch
        {
            // The card was not paid for, so it must stay sellable.
            card.RemoveMeta(SoldMeta);
            throw;
        }
    }
}
