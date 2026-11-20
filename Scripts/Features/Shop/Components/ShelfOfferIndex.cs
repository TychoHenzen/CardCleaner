using System.Collections.Generic;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     Maps the stocked levels of the sales shelves (groups "sales_shelves": card and product shelves, never storage
///     racks) to <see cref="ShelfOffer" /> values and back. An offer is the item a customer takes next from a level,
///     its last one, at the price its tag prints; an offer id is the level's node path.
/// </summary>
public sealed class ShelfOfferIndex
{
    private const float ApproachMeters = 0.8f;

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

    /// <summary>The point 0.8 m in front of the level (the shelf's -Z side) at the shelf's height, or null.</summary>
    public Vector3? ApproachPointFor(string offerId)
    {
        var level = LevelFor(offerId);
        return level?.GetParent() is CardShelf shelf ? Approach(shelf, level) : null;
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
                points.Add(Approach(shelf, level));
        }

        return points;
    }

    private static Vector3 Approach(CardShelf shelf, ShelfLevel level)
    {
        var point = level.GlobalPosition - shelf.GlobalBasis.Z * ApproachMeters;
        return new Vector3(point.X, shelf.GlobalPosition.Y, point.Z);
    }
}
