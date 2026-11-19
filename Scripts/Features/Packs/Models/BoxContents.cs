using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCleaner.Scripts.Features.Packs.Models;

/// <summary>
///     What a cardboard box holds, in the order it went in, and where each item lies (<see cref="BoxPacker" />). The
///     last item in is the first out. Where each item lies follows from the order alone, so a box rebuilt from its
///     saved list looks the same as before.
/// </summary>
public sealed class BoxContents
{
    private readonly BoxPacker _packer;
    private readonly List<StoredItem> _items = new();
    private readonly List<PackedItem> _placed = new();

    public BoxContents(BoxPacker packer)
    {
        _packer = packer;
    }

    /// <summary>The items in the order they went in.</summary>
    public IReadOnlyList<StoredItem> Items => _items;

    /// <summary>Where each item of <see cref="Items" /> lies, at the same index.</summary>
    public IReadOnlyList<PackedItem> Placed => _placed;

    public int Count => _items.Count;

    /// <summary>True when an item of <paramref name="shape" /> would fit now.</summary>
    public bool HasRoomFor(ItemShape shape) => _packer.Place(_placed, shape) != null;

    /// <summary>Puts <paramref name="item" /> in where it fits; false, with nothing changed, when there is no room.</summary>
    public bool TryAdd(StoredItem item)
    {
        if (_packer.Place(_placed, item.Shape) is not { } spot)
            return false;

        _items.Add(item);
        _placed.Add(spot);
        return true;
    }

    /// <summary>The index of the last item that <paramref name="accepts" /> takes, or -1 when there is none.</summary>
    public int LastIndexOf(Func<StoredItem, bool> accepts)
    {
        for (var index = _items.Count - 1; index >= 0; index--)
        {
            if (accepts(_items[index]))
                return index;
        }

        return -1;
    }

    /// <summary>
    ///     Takes out the item at <paramref name="index" />. Taking out any but the last packs the items after it again,
    ///     in their order; one that no longer fits comes out as well and is added to <paramref name="spilled" />.
    /// </summary>
    public StoredItem TakeAt(int index, ICollection<StoredItem> spilled)
    {
        var item = _items[index];
        var after = _items.Skip(index + 1).ToList();
        _items.RemoveRange(index, _items.Count - index);
        _placed.RemoveRange(index, _placed.Count - index);
        foreach (var next in after.Where(next => !TryAdd(next)))
            spilled.Add(next);

        return item;
    }

    /// <summary>Empties the box and puts <paramref name="items" /> in, in order; returns the ones that did not fit.</summary>
    public List<StoredItem> Refill(IEnumerable<StoredItem> items)
    {
        _items.Clear();
        _placed.Clear();
        return items.Where(item => !TryAdd(item)).ToList();
    }
}
