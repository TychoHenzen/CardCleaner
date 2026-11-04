using System.Collections.Generic;
using System.Linq;

namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>One cart line: an item and how many of it the player has chosen.</summary>
public sealed record CartLine(StockEntry Entry, int Quantity);

/// <summary>
///     The Stock app cart: one line per item id, merged on add and capped per line. Pure rules, no engine types.
/// </summary>
public sealed class OrderCart
{
    public const int MaxQuantity = 99;

    private readonly List<CartLine> _lines = [];

    /// <summary>The lines in the order they were first added.</summary>
    public IReadOnlyList<CartLine> Lines => _lines.AsReadOnly();

    public int Total => _lines.Sum(line => line.Entry.Price * line.Quantity);

    public bool IsEmpty => _lines.Count == 0;

    public int Quantity(string id)
    {
        var index = IndexOf(id);
        return index < 0 ? 0 : _lines[index].Quantity;
    }

    /// <summary>Adds one of the item. Returns false and leaves the cart unchanged when the item is locked,
    /// has no price, or its line is already at <see cref="MaxQuantity"/>.</summary>
    public bool TryAdd(StockEntry entry, int shopLevel)
    {
        if (entry.IsLocked(shopLevel) || entry.Price <= 0)
        {
            return false;
        }

        var index = IndexOf(entry.Id);
        if (index < 0)
        {
            _lines.Add(new CartLine(entry, 1));
            return true;
        }

        var current = _lines[index];
        if (current.Quantity >= MaxQuantity)
        {
            return false;
        }

        _lines[index] = current with { Quantity = current.Quantity + 1 };
        return true;
    }

    /// <summary>Takes one of the item out. The line is dropped when it reaches zero.
    /// An unknown id is ignored.</summary>
    public void RemoveOne(string id) => SetQuantity(id, Quantity(id) - 1);

    /// <summary>Drops the whole line for the item. An unknown id is ignored.</summary>
    public void RemoveLine(string id)
    {
        var index = IndexOf(id);
        if (index >= 0)
        {
            _lines.RemoveAt(index);
        }
    }

    /// <summary>Sets the line's quantity. Zero or less removes the line, more than <see cref="MaxQuantity"/> is
    /// clamped to it, and an unknown id is ignored.</summary>
    public void SetQuantity(string id, int quantity)
    {
        var index = IndexOf(id);
        if (index < 0)
        {
            return;
        }

        if (quantity <= 0)
        {
            _lines.RemoveAt(index);
            return;
        }

        _lines[index] = _lines[index] with { Quantity = System.Math.Min(quantity, MaxQuantity) };
    }

    public void Clear() => _lines.Clear();

    private int IndexOf(string id) => _lines.FindIndex(line => line.Entry.Id == id);
}
