using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>The scannable item props a checkout counter lays out for its front customer's basket.</summary>
public sealed class CheckoutItemTray(Node3D counter)
{
    private const string SpotPrefix = "ItemSpot";

    private List<CheckoutItemProp> _props = new();
    private Marker3D[] _spots = Array.Empty<Marker3D>();

    /// <summary>Finds the <c>ItemSpot</c> markers under the counter. Call once the counter's children exist.</summary>
    public void FindSpots() => _spots = CheckoutCounterLayout.MarkersNamed(counter, SpotPrefix);

    /// <summary>Lays one prop per item on the spots; a prop calls <paramref name="onScan" /> when interacted with.</summary>
    public void Lay(IReadOnlyList<CheckoutItem> items, Action<int> onScan) =>
        _props = CheckoutCounterLayout.LayOutItems(counter, _spots, items, onScan);

    /// <summary>Marks the prop of an item scanned. Nothing happens for an item with no prop.</summary>
    public void MarkScanned(int itemId) => _props.FirstOrDefault(prop => prop.ItemId == itemId)?.MarkScanned();

    /// <summary>Frees every prop.</summary>
    public void Clear()
    {
        _props.ForEach(prop => prop.QueueFree());
        _props.Clear();
    }
}
