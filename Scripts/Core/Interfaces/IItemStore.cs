using CardCleaner.Scripts.Features.Packs.Models;

namespace CardCleaner.Scripts.Core.Interfaces;

/// <summary>
///     A place that keeps cards and containers as an ordered list of records: the hand, an open cardboard box, a pile,
///     a deck or a shelf level. Every move between two of them goes through these five members, so one transfer rule
///     serves them all.
/// </summary>
public interface IItemStore
{
    /// <summary>How many items the store holds.</summary>
    int Count { get; }

    /// <summary>
    ///     The item a take returns next, without taking it; null when the store is empty. For most stores that is the
    ///     item put in last. The hand returns its front item instead, which need not be the last one it took.
    /// </summary>
    StoredItem? Peek();

    /// <summary>Whether <see cref="Put" /> would accept <paramref name="item" /> now.</summary>
    bool CanPut(StoredItem item);

    /// <summary>Puts <paramref name="item" /> in; returns false, and changes nothing, when it is refused.</summary>
    bool Put(StoredItem item);

    /// <summary>Takes out the item <see cref="Peek" /> names and returns it; null when the store is empty.</summary>
    StoredItem? TakeLast();
}
