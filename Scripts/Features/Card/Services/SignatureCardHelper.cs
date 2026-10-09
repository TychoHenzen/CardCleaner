using System;
using System.Linq;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Card.Services;

public static class SignatureCardHelper
{
    public static ulong ComputeSeed(CardSignature signature) => CardSignatureHash.Of(signature);

    public static CardRarity DetermineRarity(CardSignature[] signature)
    {
        // The scorer rewards distance from 0.5, so an all-zero (common) signature would otherwise score Legendary.
        if (signature.All(sig => !sig.HasMagicalPotential()))
            return CardRarity.Common;

        return RarityForScore(RarityScore(signature));
    }

    private static float RarityScore(CardSignature[] signature)
    {
        var totalPoints = signature.Sum(sig => sig.Elements.Sum(e => PointsForElement(e)));
        var maxPoints = signature.Length * 8 * 8;
        var rarityRatio = (float)totalPoints / maxPoints;
        return (float)Math.Log10(1f + 9f * rarityRatio);
    }

    private static int PointsForElement(float element)
    {
        var v = Mathf.Abs(Math.Abs(element) - 0.5f);
        return v switch
        {
            < 0.1f => 0,
            < 0.2f => 1,
            < 0.3f => 2,
            < 0.4f => 4,
            _ => 8
        };
    }

    private static CardRarity RarityForScore(float logScaled)
    {
        return logScaled switch
        {
            < 0.70f => CardRarity.Common,
            < 0.80f => CardRarity.Uncommon,
            < 0.92f => CardRarity.Rare,
            < 0.96f => CardRarity.Epic,
            _ => CardRarity.Legendary
        };
    }

    public static void Apply(RandomNumberGenerator rng, LayerData layer, Texture2D[] options)
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