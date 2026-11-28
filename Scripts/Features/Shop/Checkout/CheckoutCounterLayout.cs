using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Checkout;

/// <summary>Places a checkout counter's item props and marker spots.</summary>
public static class CheckoutCounterLayout
{
    /// <summary>The space left between two items stacked on one spot.</summary>
    public const float StackGap = 0.01f;

    /// <summary>
    ///     Adds one prop per item to <paramref name="counter" />, stacked on the item spots in order. Item i shows
    ///     <paramref name="looks" />[i] when there is one; every look that gets no prop is discarded.
    /// </summary>
    public static List<CheckoutItemProp> LayOutItems(Node3D counter, Marker3D[] itemSpots,
        IReadOnlyList<CheckoutItem> items, IReadOnlyList<StockItemLook?> looks, Action<int> onScan)
    {
        var props = new List<CheckoutItemProp>();
        var stacked = new float[Math.Max(itemSpots.Length, 1)];
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var spot = itemSpots.Length == 0 ? 0 : item.Id % itemSpots.Length;
            var prop = new CheckoutItemProp
            {
                ItemId = item.Id,
                Spot = spot,
                OnScan = onScan,
                Look = index < looks.Count ? looks[index] : null
            };
            counter.AddChild(prop);
            var origin = itemSpots.Length == 0 ? counter.GlobalPosition : itemSpots[spot].GlobalPosition;
            prop.GlobalPosition = origin + Vector3.Up * stacked[spot];
            stacked[spot] += prop.Size.Y + StackGap;
            props.Add(prop);
        }

        for (var index = items.Count; index < looks.Count; index++)
            looks[index]?.Discard();

        return props;
    }

    /// <summary>One checkout item per basket entry, numbered in basket order.</summary>
    public static List<CheckoutItem> ItemsOf(ICheckoutCustomer customer) => customer.Basket.Items
        .Select((offer, index) => new CheckoutItem(index, offer.AskingPrice))
        .ToList();

    /// <summary>Marker3D children of <paramref name="parent" /> named prefix plus an index, in index order.</summary>
    public static Marker3D[] MarkersNamed(Node parent, string prefix) => parent.GetChildren()
        .OfType<Marker3D>()
        .Select(marker => (Marker: marker, Index: IndexAfter(marker.Name, prefix)))
        .Where(pair => pair.Index >= 0)
        .OrderBy(pair => pair.Index)
        .Select(pair => pair.Marker)
        .ToArray();

    private static int IndexAfter(StringName name, string prefix)
    {
        var text = name.ToString();
        if (!text.StartsWith(prefix, StringComparison.Ordinal))
            return -1;

        return int.TryParse(text.AsSpan(prefix.Length), out var index) ? index : -1;
    }
}
