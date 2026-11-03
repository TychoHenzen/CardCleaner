using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>Places a checkout counter's item props and marker spots, and formats its status label.</summary>
public static class CheckoutCounterLayout
{
    private const float PropStackHeight = 0.07f;

    /// <summary>Adds one prop per item to <paramref name="counter" />, stacked on the item spots in order.</summary>
    public static List<CheckoutItemProp> LayOutItems(
        Node3D counter, Marker3D[] itemSpots, IReadOnlyList<CheckoutItem> items, Action<int> onScan)
    {
        var props = new List<CheckoutItemProp>();
        var stacked = new int[Math.Max(itemSpots.Length, 1)];
        foreach (var item in items)
        {
            var spot = itemSpots.Length == 0 ? 0 : item.Id % itemSpots.Length;
            var prop = new CheckoutItemProp { ItemId = item.Id, OnScan = onScan };
            counter.AddChild(prop);
            var origin = itemSpots.Length == 0 ? counter.GlobalPosition : itemSpots[spot].GlobalPosition;
            prop.GlobalPosition = origin + Vector3.Up * (PropStackHeight * stacked[spot]);
            stacked[spot]++;
            props.Add(prop);
        }

        return props;
    }

    /// <summary>Marker3D children of <paramref name="parent" /> named prefix plus an index, in index order.</summary>
    public static Marker3D[] MarkersNamed(Node parent, string prefix) => parent.GetChildren()
        .OfType<Marker3D>()
        .Select(marker => (Marker: marker, Index: IndexAfter(marker.Name, prefix)))
        .Where(pair => pair.Index >= 0)
        .OrderBy(pair => pair.Index)
        .Select(pair => pair.Marker)
        .ToArray();

    /// <summary>The balance line, then the last message on a second line when there is one.</summary>
    public static string StatusText(IMoneyService? money, string message)
    {
        var balance = money is null ? "Balance: -" : $"Balance: {money.Balance}";
        return string.IsNullOrEmpty(message) ? balance : $"{balance}\n{message}";
    }

    private static int IndexAfter(StringName name, string prefix)
    {
        var text = name.ToString();
        if (!text.StartsWith(prefix, StringComparison.Ordinal))
            return -1;

        return int.TryParse(text.AsSpan(prefix.Length), out var index) ? index : -1;
    }
}
