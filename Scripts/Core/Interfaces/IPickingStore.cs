using System;
using CardCleaner.Scripts.Features.Packs.Models;

namespace CardCleaner.Scripts.Core.Interfaces;

/// <summary>
///     A store whose items can also be taken out of order: the last one a target accepts. A held open cardboard box is
///     the source of a click this way, so a click on a rack level stocks the box's last booster box even when cards lie
///     on top of it.
/// </summary>
// ASSUMPTION: the five members of IItemStore (design D5) cannot name "the last item the store accepts", which the
// item-transfer spec requires of a held open box, so the box adds these two (Risk Low: only CartonStorage implements
// it).
public interface IPickingStore : IItemStore
{
    /// <summary>The last item <paramref name="accepts" /> allows, without taking it; null when there is none.</summary>
    StoredItem? PeekLast(Func<StoredItem, bool> accepts);

    /// <summary>Takes out the item <see cref="PeekLast" /> names and returns it; null when there is none.</summary>
    StoredItem? TakeLast(Func<StoredItem, bool> accepts);
}
