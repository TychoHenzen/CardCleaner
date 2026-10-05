using System;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Services;

/// <summary>
///     Sells one card: pays <see cref="CardPricing" /> into the money service and removes the card
///     from the world. Something that cannot be sold is left exactly where it is.
/// </summary>
public static class CardSale
{
    private const string SoldMeta = "sold";

    /// <summary>
    ///     Sells <paramref name="card" />. <paramref name="release" /> detaches the card from wherever it is
    ///     held (a shelf slot or the player's hand) and runs only once the sale is certain.
    /// </summary>
    public static SaleResult TrySell(RigidBody3D? card, IMoneyService money, Action<RigidBody3D>? release = null)
    {
        if (card == null || !GodotObject.IsInstanceValid(card))
            return new SaleResult(SaleStatus.NothingToSell);

        if (card.IsQueuedForDeletion() || card.HasMeta(SoldMeta))
            return new SaleResult(SaleStatus.AlreadySold);

        var price = CardPricing.GetPrice((card as CardController)?.Signature);
        if (price <= 0)
            return new SaleResult(SaleStatus.NotSellable);

        // Mark first so a second request in the same frame cannot pay out again.
        card.SetMeta(SoldMeta, true);
        try
        {
            release?.Invoke(card);
        }
        catch
        {
            // The card was not paid for, so it must stay sellable.
            card.RemoveMeta(SoldMeta);
            throw;
        }

        money.Add(price);
        card.QueueFree();

        ILog.Print($"Card sold for {price}, balance now {money.Balance}");
        return new SaleResult(SaleStatus.Sold, price, money.Balance);
    }
}
