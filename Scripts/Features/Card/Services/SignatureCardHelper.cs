using System;
using System.Linq;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Card.Services;

public static class SignatureCardHelper
{
    private const ulong EffectSeedRange = 10000UL;

    public static ulong ComputeSeed(CardSignature signature)
    {
        var seed = 17UL;
        foreach (var v in signature.Elements)
            seed = seed * 23UL + (ulong)Mathf.RoundToInt(v * 1000);
        return seed;
    }

    /// <summary>
    ///     The seed that places a card's scratches, from its signature alone. It is kept below
    ///     <see cref="EffectSeedRange" /> because the shader feeds it to <c>sin(seed * 0.0001)</c>, which loses its
    ///     precision on a large float and would scatter the same scratches differently on different GPUs.
    /// </summary>
    public static float EffectSeed(CardSignature signature)
    {
        return ComputeSeed(signature) % EffectSeedRange;
    }

    public static CardRarity DetermineRarity(CardSignature[] signature)
    {
        // The scorer rewards distance from 0.5, so an all-zero (common) signature would otherwise score Legendary.
        if (signature.All(sig => !sig.HasMagicalPotential()))
            return CardRarity.Common;

        var totalPoints = signature.Sum(sig => sig.Elements.Sum(e =>
        {
            var v = Mathf.Abs(Math.Abs(e) - 0.5f);
            return v switch
            {
                < 0.1f => 0,
                < 0.2f => 1,
                < 0.3f => 2,
                < 0.4f => 4,
                _ => 8
            };
        }));
        var maxPoints = signature.Length * 8 * 8;
        var rarityRatio = (float)totalPoints / maxPoints;
        var logScaled = (float)Math.Log10(1f + 9f * rarityRatio);

        return logScaled switch
        {
            < 0.70f => CardRarity.Common,
            < 0.80f => CardRarity.Uncommon,
            < 0.92f => CardRarity.Rare,
            < 0.96f => CardRarity.Epic,
            _ => CardRarity.Legendary
        };
    }

    public static void Apply(RandomNumberGenerator rng, Core.Data.LayerData layer, Texture2D[] options)
    {
        if (options == null || options.Length == 0) return;
        var idx = options.Length == 1
            ? 0
            : rng.RandiRange(0, options.Length - 1);
        layer.Texture = options[idx];
    }

    public static T SelectWeighted<T>(
        RandomNumberGenerator rng,
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