using System;
using System.Linq;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Card.Services;

public static class SignatureCardHelper
{
    public static int ComputeSeed(Models.CardSignature signature)
    {
        var seed = 17;
        foreach (var v in signature.Elements)
            seed = seed * 23 + Mathf.RoundToInt(v * 1000);
        return seed;
    }

    public static CardRarity DetermineRarity(Models.CardSignature signature)
    {
        var totalPoints = signature.Elements.Sum(e =>
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
        });
        var maxPoints = signature.Elements.Length * 8;
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