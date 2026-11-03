using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     The shelf items a customer is carrying. A body is held out of the scene tree while carried, so the slot's
///     Area cannot claim it again.
/// </summary>
internal sealed class CarriedStock
{
    private readonly List<Carried> _items = new();

    private sealed record Carried(RigidBody3D Body, DeckSlot Slot, ShelfOffer Offer);

    public int Count => _items.Count;

    /// <summary>The offers of everything carried, in the order it was taken.</summary>
    public IReadOnlyList<ShelfOffer> Offers() => _items.Select(item => item.Offer).ToList();

    /// <summary>Takes the slot's first card. False when the slot does not give it up.</summary>
    public bool TryTake(DeckSlot slot, ShelfOffer offer)
    {
        var body = slot.Cards[0];
        if (!slot.ReleaseCard(body))
            return false;

        body.GetParent()?.RemoveChild(body);
        _items.Add(new Carried(body, slot, offer));
        return true;
    }

    /// <summary>Puts every item back in its slot; one whose slot is gone or refuses it is freed.</summary>
    public void ReturnToSlots()
    {
        foreach (var item in Valid())
        {
            if (!GodotObject.IsInstanceValid(item.Slot))
            {
                item.Body.Free();
                continue;
            }

            item.Slot.AddChild(item.Body);
            if (!item.Slot.TryLock(item.Body))
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
