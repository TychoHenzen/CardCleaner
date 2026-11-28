using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Checkout;

/// <summary>The scannable item props a checkout counter lays out for its front customer's basket.</summary>
public sealed class CheckoutItemTray(Node3D counter)
{
    private const string SpotPrefix = "ItemSpot";

    private List<CheckoutItemProp> _props = new();
    private Marker3D[] _spots = Array.Empty<Marker3D>();

    /// <summary>Finds the <c>ItemSpot</c> markers under the counter. Call once the counter's children exist.</summary>
    public void FindSpots() => _spots = CheckoutCounterLayout.MarkersNamed(counter, SpotPrefix);

    /// <summary>
    ///     Lays one prop per item on the spots, showing the matching look where there is one; a prop calls
    ///     <paramref name="onScan" /> when interacted with.
    /// </summary>
    public void Lay(IReadOnlyList<CheckoutItem> items, IReadOnlyList<StockItemLook?> looks, Action<int> onScan) =>
        _props = CheckoutCounterLayout.LayOutItems(counter, _spots, items, looks, onScan);

    /// <summary>
    ///     Marks the prop of an item scanned, which bags it, and lets the items stacked on it settle into its place.
    ///     Nothing happens for an item with no prop.
    /// </summary>
    public void MarkScanned(int itemId)
    {
        if (_props.FirstOrDefault(prop => prop.ItemId == itemId) is not { } bagged)
            return;

        var drop = bagged.Size.Y + CheckoutCounterLayout.StackGap;
        foreach (var above in _props.Where(prop =>
                     prop.Spot == bagged.Spot && !prop.Scanned && prop.RestHeight > bagged.RestHeight))
            above.SettleBy(drop);
        bagged.MarkScanned();
    }

    /// <summary>Frees every prop.</summary>
    public void Clear()
    {
        _props.ForEach(prop => prop.QueueFree());
        _props.Clear();
    }
}
