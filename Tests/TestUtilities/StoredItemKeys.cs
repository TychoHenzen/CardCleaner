using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CardCleaner.Scripts.Features.Packs.Models;

namespace CardCleaner.Tests.TestUtilities;

/// <summary>
///     Text keys for what a cardboard box holds, so two lists of <see cref="StoredItem" /> compare by value: a card's
///     signature is an array, which record equality compares by reference.
/// </summary>
internal static class StoredItemKeys
{
    public static List<string> Of(IEnumerable<StoredItem> items) => items.Select(Of).ToList();

    public static string Of(StoredItem item) => item switch
    {
        StoredCard card => $"card {Signature(card)} look {card.LookSeed} price {card.Price}",
        StoredContainer inner => $"{inner.Kind} {inner.ScenePath} seed {inner.Seed} price {inner.Price}",
        _ => item.ToString()
    };

    private static string Signature(StoredCard card) =>
        card.Signature is { } signature
            ? string.Join(",", signature.Elements.Select(e => e.ToString("R", CultureInfo.InvariantCulture)))
            : "none";
}
