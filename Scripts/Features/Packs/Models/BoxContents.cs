using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace CardCleaner.Scripts.Features.Packs.Models;

/// <summary>
///     What a cardboard box holds, in the order it went in, and where each item lies (<see cref="BoxPacker" />). The
///     last item in is the first out. Where each item lies follows from the order alone, so a box rebuilt from its
///     saved list looks the same as before. An immutable value: every change returns new contents.
/// </summary>
public sealed record BoxContents
{
    private readonly BoxPacker _packer;
    private readonly ImmutableList<StoredItem> _items;
    private readonly ImmutableList<PackedItem> _placed;

    private BoxContents(BoxPacker packer, ImmutableList<StoredItem> items, ImmutableList<PackedItem> placed)
    {
        _packer = packer;
        _items = items;
        _placed = placed;
    }

    /// <summary>The items in the order they went in.</summary>
    public IReadOnlyList<StoredItem> Items => _items;

    /// <summary>Where each item of <see cref="Items" /> lies, at the same index.</summary>
    public IReadOnlyList<PackedItem> Placed => _placed;

    public int Count => _items.Count;

    /// <summary>An empty box packed by <paramref name="packer" />.</summary>
    public static BoxContents Empty(BoxPacker packer) => new(packer, [], []);

    /// <summary>
    ///     An empty box packed by <paramref name="packer" /> with <paramref name="items" /> put in, in order, and the
    ///     items that did not fit.
    /// </summary>
    public static (BoxContents Contents, IReadOnlyList<StoredItem> Left) Packed(BoxPacker packer,
        IEnumerable<StoredItem> items) => Empty(packer).Adding(items);

    /// <summary>True when an item of <paramref name="shape" /> would fit now.</summary>
    public bool HasRoomFor(ItemShape shape) => _packer.Place(_placed, shape) != null;

    /// <summary>These contents with <paramref name="item" /> put in where it fits, or null when there is no room.</summary>
    public BoxContents? With(StoredItem item) =>
        _packer.Place(_placed, item.Shape) is { } spot
            ? new BoxContents(_packer, _items.Add(item), _placed.Add(spot))
            : null;

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
    ///     These contents without the item at <paramref name="index" />. Taking out any but the last packs the items
    ///     after it again, in their order; one that no longer fits comes out as well and is listed in Spilled.
    /// </summary>
    public (BoxContents Remaining, IReadOnlyList<StoredItem> Spilled) Without(int index) =>
        new BoxContents(_packer, _items.GetRange(0, index), _placed.GetRange(0, index)).Adding(_items.Skip(index + 1));

    private (BoxContents Contents, IReadOnlyList<StoredItem> Left) Adding(IEnumerable<StoredItem> items)
    {
        var contents = this;
        var left = new List<StoredItem>();
        foreach (var item in items)
        {
            if (contents.With(item) is { } next)
                contents = next;
            else
                left.Add(item);
        }

        return (contents, left);
    }
}
