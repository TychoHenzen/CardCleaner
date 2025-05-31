using System.Linq;
using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Enum;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Card.Services;

/// <summary>
///     Generates card visuals deterministically from a CardSignature.
///     Picks base, border, corners, and banner textures based on signature-derived rarity.
/// </summary>
public class SignatureCardGenerator : ICardGenerator
{
    private BaseCardType[] _baseTypes;
    private GemVisual[] _gemVisuals;
    private RarityVisual[] _rarityVisuals;

    public SignatureCardGenerator()
    {
        ServiceLocator.Get<RarityVisual[]>(visual => _rarityVisuals = visual);
        ServiceLocator.Get<BaseCardType[]>(bases => _baseTypes = bases);
        ServiceLocator.Get<GemVisual[]>(gems => _gemVisuals = gems);
    }

    public void GenerateCardRenderer(CardShaderRenderer renderer, CardSignature signature, Core.Data.CardTemplate template)
    {
        var rng = new RandomNumberGenerator
        {
            Seed = (uint)SignatureCardHelper.ComputeSeed(signature)
        };
        // 1. Determine rarity
        var rarity = SignatureCardHelper.DetermineRarity(signature);

        // 2. Apply per‐rarity visuals
        var visuals = _rarityVisuals.FirstOrDefault(rv => rv.Rarity == rarity);
        if (visuals != null)
        {
            SignatureCardHelper.Apply(rng, template.CardBase, visuals.BaseOptions);
            SignatureCardHelper.Apply(rng, template.Border, visuals.BorderOptions);
            SignatureCardHelper.Apply(rng, template.Corners, visuals.CornerOptions);
            SignatureCardHelper.Apply(rng, template.Banner, visuals.BannerOptions);
            SignatureCardHelper.Apply(rng, template.ImageBackground, visuals.ImageBackgroundOptions);
            SignatureCardHelper.Apply(rng, template.DescriptionBox, visuals.DescriptionBoxOptions);
            SignatureCardHelper.Apply(rng, template.EnergyContainer, visuals.EnergyContainerOptions);
        }

        // 3. Select matching BaseCardType
        var candidates = _baseTypes
            .Where(bt => bt.CanMatch(signature))
            .ToArray();
        if (candidates.Length > 0)
        {
            var chosenBase =
                SignatureCardHelper.SelectWeighted(rng, candidates, bt => bt.CalculateMatchWeight(signature));
            SignatureCardHelper.Apply(rng, template.Art, chosenBase.ArtOptions);
            SignatureCardHelper.Apply(rng, template.Symbol, chosenBase.SymbolOptions);
            SignatureCardHelper.Apply(rng, template.EnergyFill1, chosenBase.EnergyFill1Options);
            SignatureCardHelper.Apply(rng, template.EnergyFill2, chosenBase.EnergyFill2Options);
        }

        // 4. Assign gems by dominant aspect
        for (var i = 0; i < template.GemSockets.Length; i++)
        {
            var element = (Element)i;
            var rawValue = signature[element];
            var intensity = Mathf.Abs(rawValue); // 0..1
            var isPos = rawValue >= 0;

            var gemVis = _gemVisuals.FirstOrDefault(gv => gv.Element == element);
            if (gemVis != null)
            {
                SetGemVisuals(renderer, template, gemVis, isPos, i, intensity);
            }
        }

        renderer.NameLabel.Text = rarity.ToString();
        renderer.AttrLabel.Text = signature.ToString();
    }

    private static void SetGemVisuals(CardShaderRenderer renderer, CardTemplate template, GemVisual gemVis, bool isPos,
        int i, float intensity)
    {
        // Select textures based on sign
        var socketTex = gemVis.SocketTexture;
        var gemTex = isPos
            ? gemVis.PositiveGemTexture
            : gemVis.NegativeGemTexture;
        template.GemSockets[i].Texture = socketTex;
        template.Gems[i].Texture = gemTex;

        // Select emission settings based on sign and scale by intensity
        var tint = isPos
            ? gemVis.PositiveEmissionColor
            : gemVis.NegativeEmissionColor;
        var strength = intensity * (isPos
            ? gemVis.PositiveEmissionStrength
            : gemVis.NegativeEmissionStrength);

        renderer.SetGemEmission(i, tint, strength);
    }
}