using System;
using System.Linq;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Interfaces;
using Godot;
using Godot.Collections;

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

    private Dictionary<CardRarity, int> raritiesSpawned = new()
    {
        { CardRarity.Common, 0 },
        { CardRarity.Uncommon, 0 },
        { CardRarity.Rare, 0 },
        { CardRarity.Epic, 0 },
        { CardRarity.Legendary, 0 },
    };

    public SignatureCardGenerator(
        RarityVisual[] rarityVisuals,
        BaseCardType[] baseTypes,
        GemVisual[] gemVisuals)
    {
        _rarityVisuals = rarityVisuals;
        _baseTypes = baseTypes;
        _gemVisuals = gemVisuals;
    }

    public void GenerateCardRenderer(CardShaderRenderer renderer, CardSignature signature, CardTemplate template)
    {
        var rng = new RandomNumberGenerator
        {
            Seed = (uint)SignatureCardHelper.ComputeSeed(signature)
        };
        // 1. Determine rarity
        var rarity = SignatureCardHelper.DetermineRarity(signature);
        raritiesSpawned[rarity]++;

        // Build totals string in enum order: Common, Uncommon, Rare, Epic, Legendary
        var totalsString = string.Join(",",
            raritiesSpawned
                .OrderBy(kv => kv.Key)
                .Select(kv => kv.Value)
        );
        GD.Print($"[SignatureGenerator] Rarity: {rarity} (Totals: {totalsString})");


        // 2. Apply per‐rarity visuals
        var visuals = _rarityVisuals.FirstOrDefault(rv => rv.Rarity == rarity);
        if (visuals != null)
        {
            SignatureCardHelper.Apply(rng,template.CardBase, visuals.BaseOptions);
            SignatureCardHelper.Apply(rng,template.Border, visuals.BorderOptions);
            SignatureCardHelper.Apply(rng,template.Corners, visuals.CornerOptions);
            SignatureCardHelper.Apply(rng,template.Banner, visuals.BannerOptions);
            SignatureCardHelper.Apply(rng, template.ImageBackground,    visuals.ImageBackgroundOptions);
            SignatureCardHelper.Apply(rng, template.DescriptionBox,     visuals.DescriptionBoxOptions);
            SignatureCardHelper.Apply(rng, template.EnergyContainer,    visuals.EnergyContainerOptions);

        }

        // 3. Select matching BaseCardType
        var candidates = _baseTypes
            .Where(bt => bt.CanMatch(signature))
            .ToArray();
        if (candidates.Length > 0)
        {
            var chosenBase = SignatureCardHelper.SelectWeighted(rng,candidates, bt => bt.CalculateMatchWeight(signature));
            SignatureCardHelper.Apply(rng, template.Art, chosenBase.ArtOptions);
            SignatureCardHelper.Apply(rng, template.Symbol, chosenBase.SymbolOptions);
            SignatureCardHelper.Apply(rng, template.EnergyFill1, chosenBase.EnergyFill1Options);
            SignatureCardHelper.Apply(rng, template.EnergyFill2, chosenBase.EnergyFill2Options);
        }

        // 4. Assign gems by dominant aspect
        for (int i = 0; i < template.GemSockets.Length; i++)
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
                template.GemSockets[i].Texture = socketTex;
                template.Gems[i].Texture       = gemTex;

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

        renderer.NameLabel.Text = rarity.ToString();


    }


}