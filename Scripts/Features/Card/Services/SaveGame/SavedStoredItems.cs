using System;
using System.Globalization;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Packs.Models;

namespace CardCleaner.Scripts.Features.Card.Services.SaveGame;

/// <summary>
///     The slot record of one stored item, the way a cardboard box, a deck and a pile keep it: a card (signature, look
///     seed) or a booster or booster box (scene, kind, seed), each with the price the player set on it.
/// </summary>
internal static class SavedStoredItems
{
    private const string CardItem = "card";
    private const string ContainerItem = "container";
    private const int SignatureLength = 8;

    /// <summary>One stored item as it is written to a slot.</summary>
    internal sealed class Record
    {
        public string Type { get; set; } = "";
        public float[]? Signature { get; set; }
        public int? LookSeed { get; set; }
        public string? ScenePath { get; set; }
        public string? Kind { get; set; }
        public string? Seed { get; set; }
        public object? Price { get; set; }
    }

    internal static Record RecordOf(StoredItem item) => item switch
    {
        StoredCard card => new Record
        {
            Type = CardItem,
            Signature = card.Signature?.Elements,
            LookSeed = card.LookSeed,
            Price = card.Price
        },
        StoredContainer inner => new Record
        {
            Type = ContainerItem,
            ScenePath = inner.ScenePath,
            Kind = inner.Kind.ToString(),
            Seed = inner.Seed?.ToString(CultureInfo.InvariantCulture),
            Price = inner.Price
        },
        _ => throw new ArgumentOutOfRangeException(nameof(item), item, "Unknown stored item")
    };

    /// <summary>
    ///     The item <paramref name="record" /> describes. Throws <see cref="FormatException" />, naming
    ///     <paramref name="holder" /> ("Cardboard box from ...", "A deck"), when it cannot be read.
    /// </summary>
    internal static StoredItem Parse(Record record, string holder) => record.Type switch
    {
        CardItem => ParseCard(record, holder),
        ContainerItem => ParseContainer(record, holder),
        _ => throw new FormatException($"{holder} holds an item of unknown type '{record.Type}'")
    };

    /// <summary>The card <paramref name="record" /> describes; anything but a card is malformed.</summary>
    internal static StoredCard ParseCard(Record record, string holder)
    {
        if (record.Type != CardItem)
            throw new FormatException($"{holder} holds an item of type '{record.Type}', not a card");

        return new StoredCard(ParseSignature(record, holder), record.LookSeed, ParsePrice(record.Price, "card"));
    }

    /// <summary>
    ///     A price is a positive whole number. Anything else (absent, null, corrupt) leaves the item at its market price,
    ///     as a card's corrupt price does, so one bad price never stops the item from coming back.
    /// </summary>
    internal static int? ParsePrice(object? saved, string what)
    {
        if (saved is null)
            return null;

        if (saved is long or int && Convert.ToInt64(saved) is var price and > 0 and <= int.MaxValue)
            return (int)price;

        ILog.Warning($"Saved {what} price '{saved}' is not a positive whole number; the market price is used");
        return null;
    }

    private static CardSignature? ParseSignature(Record record, string holder)
    {
        if (record.Signature == null)
            return null;

        if (record.Signature.Length != SignatureLength || record.Signature.Any(value => !float.IsFinite(value)))
            throw new FormatException($"{holder} holds a card with an unreadable signature");

        return new CardSignature(record.Signature);
    }

    // A box holds booster boxes and boosters, never another cardboard box.
    private static StoredContainer ParseContainer(Record record, string holder)
    {
        if (string.IsNullOrEmpty(record.ScenePath))
            throw new FormatException($"{holder} holds a container with no scene");

        if (!Enum.TryParse<CardContainerKind>(record.Kind, out var kind)
            || kind is not (CardContainerKind.Pack or CardContainerKind.Booster))
            throw new FormatException($"{holder} holds a container of kind '{record.Kind}'");

        ulong? seed = null;
        if (record.Seed != null)
        {
            if (!ulong.TryParse(record.Seed, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
                throw new FormatException($"{holder} holds a container with seed '{record.Seed}'");
            seed = parsed;
        }

        return new StoredContainer(record.ScenePath, kind, seed, ParsePrice(record.Price, "container"));
    }
}
