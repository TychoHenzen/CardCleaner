using System;
using System.Linq;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Card.Services;

public static class SignatureCardHelper
{
    public static ulong ComputeSeed(CardSignature signature) => CardSignatureHash.Of(signature);

    /// <summary>An element whose magnitude reaches this is pure.</summary>
    public const float PureThreshold = 0.9f;

    /// <summary>
    ///     Rarity is the number of pure elements: none Common, one Uncommon, two Rare, three Epic, four or more
    ///     Legendary. Strength is the magnitude on the -1 to +1 range, so the sign never matters and a stronger element
    ///     never lowers the count.
    /// </summary>
    public static CardRarity DetermineRarity(CardSignature[] signature)
    {
        // ASSUMPTION: every caller passes one card's signature; with several, their pure elements add up.
        var pure = signature.Sum(sig => sig.Elements.Count(e => MathF.Abs(e) >= PureThreshold));
        return pure switch
        {
            0 => CardRarity.Common,
            1 => CardRarity.Uncommon,
            2 => CardRarity.Rare,
            3 => CardRarity.Epic,
            _ => CardRarity.Legendary
        };
    }

    public static void Apply(IRandom rng, LayerData layer, Texture2D[] options)
    {
        if (options == null || options.Length == 0) return;
        var idx = options.Length == 1
            ? 0
            : rng.RandiRange(0, options.Length - 1);
        layer.Texture = options[idx];
    }

    /// <summary>Picks one item with equal chance. <paramref name="items" /> must not be empty.</summary>
    public static T PickUniform<T>(IRandom rng, T[] items)
    {
        return items.Length == 1 ? items[0] : items[rng.RandiRange(0, items.Length - 1)];
    }

    public static T SelectWeighted<T>(
        IRandom rng,
        T[] items,
        Func<T, float> weightFn)
    {
        var weights = items.Select(weightFn).ToArray();
        var total = weights.Sum();
        if (total <= 0f) return items[0];

        var pick = rng.Randf() * total;
        var cum = 0f;
        for (var i = 0; i < items.Length; i++)
        {
            cum += weights[i];
            if (pick <= cum)
                return items[i];
        }

        return items[^1];
    }
}
