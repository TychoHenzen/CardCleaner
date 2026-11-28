using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Card.Components;
using Godot;
using CardCleaner.Scripts.Features.Shop.Checkout;
using CardCleaner.Scripts.Features.Shop.Shelves;

namespace CardCleaner.Scripts.Features.Shop.Customers;

/// <summary>
///     The shelf items a customer is carrying. A body is held out of the scene tree while carried, so the level's
///     triggers cannot claim it again.
/// </summary>
internal sealed class CarriedStock
{
    private readonly List<Carried> _items = new();

    private sealed record Carried(RigidBody3D Body, ShelfLevel Level, ShelfOffer Offer);

    public int Count => _items.Count;

    /// <summary>The offers of everything carried, in the order it was taken.</summary>
    public IReadOnlyList<ShelfOffer> Offers() => _items.Select(item => item.Offer).ToList();

    /// <summary>A still copy of every carried item, in the order of <see cref="Offers" />.</summary>
    public List<StockItemLook?> Looks() => _items
        .Select(item => GodotObject.IsInstanceValid(item.Body) ? StockItemLook.Capture(item.Body) : null)
        .ToList();

    /// <summary>
    ///     Takes the level's last item, the one put on last, so the rest stays where it lies. False when the level is
    ///     empty or its seat does not give it up.
    /// </summary>
    public bool TryTake(ShelfLevel level, ShelfOffer offer)
    {
        if (level.Last is not { } body || body.GetParent() is not DeckSlot seat || !seat.ReleaseCard(body))
            return false;

        seat.RemoveChild(body);
        _items.Add(new Carried(body, level, offer));
        return true;
    }

    /// <summary>Puts every item back on its level; one whose level is gone or refuses it is freed.</summary>
    public void ReturnToSlots()
    {
        foreach (var item in Valid())
        {
            if (!GodotObject.IsInstanceValid(item.Level))
            {
                item.Body.Free();
                continue;
            }

            // In the tree under the level first: a level takes only a body in play.
            item.Level.AddChild(item.Body);
            if (!item.Level.TryLock(item.Body))
                item.Body.Free();
        }

        _items.Clear();
    }

    // The carried bodies are out of the tree, so QueueFree would still free them, but Free is exact and immediate.
    public void Free()
    {
        foreach (var item in Valid())
            item.Body.Free();

        _items.Clear();
    }

    private List<Carried> Valid() => _items.Where(item => GodotObject.IsInstanceValid(item.Body)).ToList();
}
