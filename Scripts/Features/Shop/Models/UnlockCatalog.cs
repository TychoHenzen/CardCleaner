using System;
using System.Collections.Generic;

namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>What an unlock adds to the shop once its level is reached.</summary>
public enum UnlockKind
{
    Product,
    Furniture,
    Expansion,
    Staff,
}

/// <summary>One unlockable thing: its catalog id, what it is, and the shop level that unlocks it.</summary>
public sealed record UnlockEntry(string Id, UnlockKind Kind, int RequiredLevel);

/// <summary>
///     The rows that say which shop level unlocks which item id. Pure rules, no engine types. An id with no row
///     has no unlock level, so the shop treats it as locked.
/// </summary>
public sealed class UnlockCatalog
{
    private readonly Dictionary<string, UnlockEntry> _byId;

    /// <summary>
    ///     Builds the catalog from its rows. Throws <see cref="ArgumentException" /> on an empty or whitespace id, a
    ///     required level below 1, or a duplicate id.
    /// </summary>
    public UnlockCatalog(IEnumerable<UnlockEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _byId = new Dictionary<string, UnlockEntry>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Id))
                throw new ArgumentException("An unlock needs a non-empty id.", nameof(entries));
            if (entry.RequiredLevel < 1)
                throw new ArgumentException($"Unlock '{entry.Id}' requires a level below 1.", nameof(entries));
            if (!_byId.TryAdd(entry.Id, entry))
                throw new ArgumentException($"Duplicate unlock id '{entry.Id}'.", nameof(entries));
        }
    }

    /// <summary>
    ///     The rows of the shop as it ships. Later slices (#404, #406, #407, #408) add their rows here; an id
    ///     without a row is locked.
    /// </summary>
    public static UnlockCatalog Default => new UnlockCatalog(new[]
    {
        // ASSUMPTION: level 1 for the seven ids that are open today keeps today's behavior; the levels are data.
        new UnlockEntry("shelf", UnlockKind.Furniture, 1),
        new UnlockEntry("cardboard_box", UnlockKind.Product, 1),
        // ASSUMPTION: bulk_packing_station is the one row at level 2, per the issue's C2 rule; retune in data.
        new UnlockEntry("bulk_packing_station", UnlockKind.Furniture, 2),
        new UnlockEntry("product_shelf", UnlockKind.Furniture, 1),
        new UnlockEntry("storage_rack", UnlockKind.Furniture, 1),
        new UnlockEntry("conveyor", UnlockKind.Furniture, 1),
        new UnlockEntry("arcade_cabinet", UnlockKind.Furniture, 1),
        new UnlockEntry("wiring", UnlockKind.Product, 1),
    });

    /// <summary>The level that unlocks the id, or null when the id has no row.</summary>
    public int? RequiredLevel(string id) => _byId.TryGetValue(id, out var entry) ? entry.RequiredLevel : null;

    public IReadOnlyCollection<UnlockEntry> Entries => _byId.Values;
}
