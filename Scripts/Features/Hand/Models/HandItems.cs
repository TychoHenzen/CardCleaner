using System;
using System.Collections.Generic;

namespace CardCleaner.Scripts.Features.Hand.Models;

/// <summary>
///     The order and capacity rules of the player's one hand, over any kind of entry (the hand node keeps its live
///     bodies here; the unit tests keep plain values). Items stay in the order they were taken, and one of them is the
///     front item that every hand action applies to. A card or a booster pack takes one place and the hand holds at
///     most <see cref="MostPlaces" />; an exclusive item (a booster box or a cardboard box) is taken only into an empty
///     hand and is then its only item.
/// </summary>
public sealed class HandItems<T> where T : notnull
{
    /// <summary>The most places the hand holds.</summary>
    public const int MostPlaces = 100;

    private readonly List<T> _items = new();
    private readonly Func<T, bool> _isExclusive;

    /// <param name="isExclusive">Whether an entry takes the whole hand (a booster box or a cardboard box).</param>
    public HandItems(Func<T, bool> isExclusive)
    {
        _isExclusive = isExclusive;
    }

    /// <summary>The items in the order they were taken.</summary>
    public IReadOnlyList<T> Items => _items;

    public int Count => _items.Count;

    /// <summary>The index of the front item, or -1 while the hand is empty.</summary>
    public int FrontIndex { get; private set; } = -1;

    /// <summary>The front item, or the default value while the hand is empty.</summary>
    public T? Front => FrontIndex >= 0 ? _items[FrontIndex] : default;

    /// <summary>Whether the hand's only item is a booster box or a cardboard box.</summary>
    public bool HoldsExclusive => _items.Count == 1 && _isExclusive(_items[0]);

    /// <summary>How many more one-place items the hand takes now.</summary>
    public int Room => HoldsExclusive ? 0 : MostPlaces - _items.Count;

    /// <summary>
    ///     How many one-place items the hand would take once its front item has left it: the room a booster opened in
    ///     front has for its cards. An empty hand has its whole room.
    /// </summary>
    public int RoomOnceFrontLeaves => _items.Count == 0 ? MostPlaces : MostPlaces - (_items.Count - 1);

    /// <summary>Whether <paramref name="item" /> may join the hand now.</summary>
    public bool CanAdd(T item)
    {
        if (_items.Count == 0)
            return true;
        return !_isExclusive(item) && !HoldsExclusive && _items.Count < MostPlaces;
    }

    /// <summary>
    ///     Adds <paramref name="item" /> after the items already held; returns false when it may not join.
    /// </summary>
    public bool Add(T item)
    {
        if (!CanAdd(item))
            return false;
        _items.Add(item);
        // ASSUMPTION: the front item stays where it is when an item joins; only an empty hand's first item becomes
        // the front. The hand-items spec orders new items after the held ones and names no front change on a take.
        if (FrontIndex < 0)
            FrontIndex = 0;
        return true;
    }

    /// <summary>
    ///     Removes <paramref name="item" />; returns false when the hand does not hold it. An item before the front
    ///     keeps the same front item; removing the front item makes the next item the front, wrapping to the first.
    /// </summary>
    public bool Remove(T item)
    {
        var index = _items.IndexOf(item);
        if (index < 0)
            return false;
        _items.RemoveAt(index);
        if (_items.Count == 0)
            FrontIndex = -1;
        else if (index < FrontIndex)
            FrontIndex--;
        else if (FrontIndex >= _items.Count)
            FrontIndex = 0;
        return true;
    }

    /// <summary>
    ///     Moves the front by <paramref name="step" /> items: +1 (wheel up) to the next item, -1 (wheel down) to the
    ///     previous one, wrapping at either end.
    /// </summary>
    public void Rotate(int step)
    {
        if (_items.Count < 2)
            return;
        FrontIndex = ((FrontIndex + step) % _items.Count + _items.Count) % _items.Count;
    }
}
