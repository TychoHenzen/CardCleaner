using System.Collections.Generic;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     Maps the stocked slots of the sales shelves (groups "sales_shelves": card and product shelves, never storage
///     racks) to <see cref="ShelfOffer" /> values and back. An offer id is the slot's node path.
/// </summary>
public sealed class ShelfOfferIndex
{
    private const float ApproachMeters = 0.8f;

    private readonly SceneTree _tree;

    public ShelfOfferIndex(SceneTree tree)
    {
        _tree = tree;
    }

    /// <summary>One offer per slot that holds stock with a market value above 0 and has a price tag.</summary>
    public IReadOnlyList<ShelfOffer> Collect()
    {
        var offers = new List<ShelfOffer>();
        foreach (var node in _tree.GetNodesInGroup(CardShelf.SalesGroupName))
        {
            if (node is not CardShelf shelf)
                continue;

            foreach (var slot in shelf.Slots)
            {
                var pricing = shelf.PricingFor(slot);
                if (pricing is null || !slot.HasCards || pricing.MarketValue <= 0)
                    continue;

                offers.Add(new ShelfOffer(slot.GetPath().ToString(), pricing.AskingValue, pricing.MarketValue));
            }
        }

        return offers;
    }

    /// <summary>The slot an offer id names, or null when it is no longer in the tree.</summary>
    public DeckSlot? SlotFor(string offerId) => _tree.Root.GetNodeOrNull<DeckSlot>(new NodePath(offerId));

    /// <summary>The point 0.8 m in front of the slot (the shelf's -Z side) at the shelf's height, or null.</summary>
    public Vector3? ApproachPointFor(string offerId)
    {
        var slot = SlotFor(offerId);
        return slot?.GetParent() is CardShelf shelf ? Approach(shelf, slot) : null;
    }

    /// <summary>
    ///     The approach point of every slot of every shown sales shelf, stocked or not, in tree order: where a
    ///     customer with nothing to buy stands to look. A shelf being carried (hidden) has none.
    /// </summary>
    public IReadOnlyList<Vector3> BrowsePoints()
    {
        var points = new List<Vector3>();
        foreach (var node in _tree.GetNodesInGroup(CardShelf.SalesGroupName))
        {
            if (node is not CardShelf shelf || !shelf.IsVisibleInTree())
                continue;

            foreach (var slot in shelf.Slots)
                points.Add(Approach(shelf, slot));
        }

        return points;
    }

    private static Vector3 Approach(CardShelf shelf, DeckSlot slot)
    {
        var point = slot.GlobalPosition - shelf.GlobalBasis.Z * ApproachMeters;
        return new Vector3(point.X, shelf.GlobalPosition.Y, point.Z);
    }
}
