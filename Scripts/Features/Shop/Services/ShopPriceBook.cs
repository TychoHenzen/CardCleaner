using System;
using System.Linq;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;
using Saveable;

namespace CardCleaner.Scripts.Features.Shop.Services;

/// <summary>
///     The shop's price book for boosters and packs (<see cref="ProductPriceList" />), saved with the slot under
///     UniqueID <c>prices</c>, one entry per kind the player priced. Place it in a scene under an AutoServiceProvider.
/// </summary>
[Service(typeof(IPriceBook))]
public partial class ShopPriceBook : Node, IPriceBook, ISaveable
{
    private ProductPriceList _prices = ProductPriceList.MarketPrices;

    /// <summary>The catalog the shop orders stock from; its prices are the wholesale costs.</summary>
    [Export]
    public OrderCatalog? Wholesale { get; set; }

    public StringName UniqueID => "prices";

    public event Action? Changed;

    public int PriceFor(CardContainerKind kind) => _prices.PriceFor(kind);

    public void Set(CardContainerKind kind, int price)
    {
        _prices = _prices.With(kind, price);
        Changed?.Invoke();
    }

    public int? WholesaleFor(CardContainerKind kind)
    {
        var id = ProductPriceList.IdOf(kind);
        return Wholesale?.Items.FirstOrDefault(item => item.Id == id)?.Price;
    }

    // Only the prices the player set: a kind left at its market price follows that price if it ever changes.
    public void Save(NodeSave save)
    {
        foreach (var (kind, price) in _prices.SetPrices)
            save.SetOrAddProperty(ProductPriceList.IdOf(kind), price);
    }

    // Read as object: a typed read of a corrupt value throws, and that would stop the rest of the tree load. JSON
    // numbers come back as long; a save built in memory holds int.
    public void Load(NodeSave save)
    {
        _prices = ProductPriceList.MarketPrices;
        foreach (var kind in ProductPriceList.PricedKinds)
        {
            var key = ProductPriceList.IdOf(kind);
            if (!save.TryGetProperty<object>(key, out var raw))
                continue;

            if (raw is long or int)
                _prices = _prices.With(kind, Convert.ToInt64(raw));
            else
                ILog.Warning($"Saved {key} price '{raw}' is not a whole number; the market price is used");
        }

        Changed?.Invoke();
    }
}
