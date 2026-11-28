using System.Collections.Generic;
using Godot;
using CardCleaner.Scripts.Features.Shop.Shelves;

namespace CardCleaner.Scripts.Features.Shop.Customers;

/// <summary>
///     Maps the stocked levels of the sales shelves (groups "sales_shelves": card and product shelves, never storage
///     racks) to <see cref="ShelfOffer" /> values and back. An offer is the item a customer takes next from a level,
///     its last one, at the price its tag prints; an offer id is the level's node path.
/// </summary>
public sealed class ShelfOfferIndex
{
    private readonly SceneTree _tree;

    public ShelfOfferIndex(SceneTree tree)
    {
        _tree = tree;
    }

    /// <summary>One offer per level that holds stock with a market value above 0 and has a price tag.</summary>
    public IReadOnlyList<ShelfOffer> Collect()
    {
        var offers = new List<ShelfOffer>();
        foreach (var node in _tree.GetNodesInGroup(CardShelf.SalesGroupName))
        {
            if (node is not CardShelf shelf)
                continue;

            foreach (var level in shelf.Levels)
            {
                var pricing = shelf.PricingFor(level);
                if (pricing is null || level.Count == 0 || pricing.MarketValue <= 0)
                    continue;

                offers.Add(new ShelfOffer(level.GetPath().ToString(), pricing.AskingValue, pricing.MarketValue));
            }
        }

        return offers;
    }

    /// <summary>The level an offer id names, or null when it is no longer in the tree.</summary>
    public ShelfLevel? LevelFor(string offerId) => _tree.Root.GetNodeOrNull<ShelfLevel>(new NodePath(offerId));

    /// <summary>Where a customer stands to reach the level an offer names (its stand point), or null.</summary>
    public Vector3? ApproachPointFor(string offerId)
    {
        var level = LevelFor(offerId);
        return level?.GetParent() is CardShelf ? level.StandPoint : null;
    }

    /// <summary>
    ///     The approach point of every level of every shown sales shelf, stocked or not, in tree order: where a
    ///     customer with nothing to buy stands to look. A shelf being carried (hidden) has none.
    /// </summary>
    public IReadOnlyList<Vector3> BrowsePoints()
    {
        var points = new List<Vector3>();
        foreach (var node in _tree.GetNodesInGroup(CardShelf.SalesGroupName))
        {
            if (node is not CardShelf shelf || !shelf.IsVisibleInTree())
                continue;

            foreach (var level in shelf.Levels)
                points.Add(level.StandPoint);
        }

        return points;
    }
}
