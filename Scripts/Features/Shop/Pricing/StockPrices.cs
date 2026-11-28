using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Packs.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Pricing;

/// <summary>
///     The asking price stock carries with it, read and set alike on a body and on its record: a card's
///     <see cref="CardController.ShopPrice" />, a container's <see cref="CardContainer.ShopPrice" />. Null is the
///     market price; a cardboard box has none.
/// </summary>
internal static class StockPrices
{
    public static int? Of(RigidBody3D body) => body switch
    {
        CardController card => card.ShopPrice,
        CardContainer container => container.ShopPrice,
        _ => null
    };

    public static int? Of(StoredItem item) => item switch
    {
        StoredCard card => card.Price,
        StoredContainer container => container.Price,
        _ => null
    };

    public static void Set(RigidBody3D body, int? price)
    {
        switch (body)
        {
            case CardController card:
                card.ShopPrice = price;
                break;
            case CardContainer container:
                container.ShopPrice = price;
                break;
        }
    }

    public static StoredItem With(StoredItem item, int? price) => item switch
    {
        StoredCard card => card with { Price = price },
        StoredContainer container => container with { Price = price },
        _ => item
    };
}
