using System;
using System.Linq;
using CardCleaner.Scripts.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Card.Services;

/// <summary>
/// Generates card visuals deterministically from a CardSignature.
/// Picks base, border, corners, and banner textures based on signature-derived rarity.
/// </summary>
public class SignatureCardGenerator : ICardGenerator
{
    private readonly RarityVisual[] _rarityVisuals;
    private readonly BaseCardType[] _baseTypes;
    private readonly GemVisual[] _gemVisuals;

    public SignatureCardGenerator(
        RarityVisual[] rarityVisuals,
        BaseCardType[] baseTypes,
        GemVisual[] gemVisuals)
    {
        _rarityVisuals = rarityVisuals;
        _baseTypes = baseTypes;
        _gemVisuals = gemVisuals;
    }


    public void Verify()
    {
        // No verification implemented yet
    }


    public void GenerateCardRenderer(CardShaderRenderer renderer, CardSignature signature)
    {
        
        var rng = new RandomNumberGenerator
        {
            Seed = (uint)ComputeSeed(signature)
        };
        // 1. Determine rarity
        var rarity = DetermineRarity(signature);
        GD.Print($"[SignatureGenerator] Rarity: {rarity}");

        // 2. Apply per‐rarity visuals
        var visuals = _rarityVisuals.FirstOrDefault(rv => rv.Rarity == rarity);
        if (visuals != null)
        {
            Apply(rng,renderer.CardBase, visuals.BaseOptions);
            Apply(rng,renderer.Border, visuals.BorderOptions);
            Apply(rng,renderer.Corners, visuals.CornerOptions);
            Apply(rng,renderer.Banner, visuals.BannerOptions);
        }

        // 3. Select matching BaseCardType
        var candidates = _baseTypes
            .Where(bt => bt.CanMatch(signature))
            .ToArray();
        if (candidates.Length > 0)
        {
            var chosenBase = SelectWeighted(rng,candidates, bt => bt.CalculateMatchWeight(signature));
            Apply(rng,renderer.Art, chosenBase.ArtOptions);
            Apply(rng,renderer.Symbol, chosenBase.SymbolOptions);
        }

        // 4. Assign gems by dominant aspect
        for (int i = 0; i < renderer.GemSockets.Length; i++)
        {
            var element  = (Element)i;
            var rawValue = signature[element];
            var intensity = Mathf.Abs(rawValue);       // 0..1
            bool isPos   = rawValue >= 0;

            var gemVis = _gemVisuals.FirstOrDefault(gv => gv.Element == element);
            if (gemVis != null)
            {
                // Select textures based on sign
                var socketTex = gemVis.SocketTexture;
                var gemTex    = isPos
                    ? gemVis.PositiveGemTexture
                    : gemVis.NegativeGemTexture;
                renderer.GemSockets[i].Texture = socketTex;
                renderer.Gems[i].Texture       = gemTex;

                // Select emission settings based on sign and scale by intensity
                var tint    = isPos 
                    ? gemVis.PositiveEmissionColor 
                    : gemVis.NegativeEmissionColor;
                var strength= intensity * (isPos
                    ? gemVis.PositiveEmissionStrength
                    : gemVis.NegativeEmissionStrength);

                renderer.SetGemEmission(i, tint, strength);
            }
        }


    }


    private static int ComputeSeed(CardSignature signature)
    {
        int seed = 17;
        foreach (var v in signature.Elements)
            seed = seed * 23 + Mathf.RoundToInt(v * 1000);
        return seed;
    }

    private static CardRarity DetermineRarity(CardSignature signature)
    {
        // Simple intensity-based thresholds
        float intensity = signature.Elements.Select(Math.Abs).Sum() / signature.Elements.Length;
        if (intensity < 0.5f)
            return CardRarity.Common;
        if (intensity < 0.75f)
            return CardRarity.Uncommon;
        if (intensity < 0.9f)
            return CardRarity.Rare;
        if (intensity < 0.98f)
            return CardRarity.Epic;
        return CardRarity.Legendary;
    }

        
    private static void Apply(RandomNumberGenerator rng, LayerData layer, Texture2D[] options)
    {
        if (options == null || options.Length == 0) return;
        var idx = options.Length == 1
            ? 0
            : rng.RandiRange(0, options.Length - 1);
        layer.Texture = options[idx];
    }
        

    private static T SelectWeighted<T>(RandomNumberGenerator rng, T[] items, Func<T, float> weightFn)
    {
        var weights = items.Select(weightFn).ToArray();
        float total = weights.Sum();
        if (total <= 0f) return items[0];

        float pick = rng.Randf() * total;
        float cumulative = 0f;
        for (int i = 0; i < items.Length; i++)
        {
            cumulative += weights[i];
            if (pick <= cumulative)
                return items[i];
        }
        return items[^1];
    }
}