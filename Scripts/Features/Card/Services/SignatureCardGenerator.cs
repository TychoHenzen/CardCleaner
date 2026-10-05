using System.Linq;
using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Card.Services;

/// <summary>
///     Generates card visuals deterministically from a CardSignature.
///     Picks base, border, corners, and banner textures based on signature-derived rarity.
/// </summary>
/// 
[Service(ServiceLifetime.Singleton, typeof(ICardGenerator))]
public class SignatureCardGenerator : ICardGenerator
{
    private BaseCardType[]? _baseTypes;
    private GemVisual[]? _gemVisuals;
    private RarityVisual[]? _rarityVisuals;

    public SignatureCardGenerator()
    {
        ServiceLocator.Get<RarityVisual[]>(visual => _rarityVisuals = visual);
        ServiceLocator.Get<BaseCardType[]>(bases => _baseTypes = bases);
        ServiceLocator.Get<GemVisual[]>(gems => _gemVisuals = gems);
    }

    public void GenerateCardRenderer(CardShaderRenderer renderer, CardSignature signature, CardTemplate cardTemplate)
    {
        if (_rarityVisuals == null || _baseTypes == null || _gemVisuals == null)
            return;
        var rng = new RandomNumberGenerator
        {
            Seed = (uint)SignatureCardHelper.ComputeSeed(signature)
        };
        // 1. Determine rarity
        var rarity = SignatureCardHelper.DetermineRarity(new[] { signature });

        // 2. Apply per‐rarity visuals
        var visuals = _rarityVisuals.FirstOrDefault(rv => rv.Rarity == rarity);
        if (visuals != null)
        {
            SignatureCardHelper.Apply(rng, cardTemplate.CardBase, visuals.BaseOptions);
            SignatureCardHelper.Apply(rng, cardTemplate.Border, visuals.BorderOptions);
            SignatureCardHelper.Apply(rng, cardTemplate.Corners, visuals.CornerOptions);
            SignatureCardHelper.Apply(rng, cardTemplate.Banner, visuals.BannerOptions);
            SignatureCardHelper.Apply(rng, cardTemplate.ImageBackground, visuals.ImageBackgroundOptions);
            SignatureCardHelper.Apply(rng, cardTemplate.DescriptionBox, visuals.DescriptionBoxOptions);
            SignatureCardHelper.Apply(rng, cardTemplate.EnergyContainer, visuals.EnergyContainerOptions);
        }

        // 3. Select matching BaseCardType
        var candidates = _baseTypes
            .Where(bt => bt.CanMatch(signature))
            .ToArray();
        if (candidates.Length > 0)
        {
            var chosenBase =
                SignatureCardHelper.SelectWeighted(rng, candidates, bt => bt.CalculateMatchWeight(signature));
            SignatureCardHelper.Apply(rng, cardTemplate.Art, chosenBase.ArtOptions);
            SignatureCardHelper.Apply(rng, cardTemplate.Symbol, chosenBase.SymbolOptions);
            SignatureCardHelper.Apply(rng, cardTemplate.EnergyFill1, chosenBase.EnergyFill1Options);
            SignatureCardHelper.Apply(rng, cardTemplate.EnergyFill2, chosenBase.EnergyFill2Options);
        }

        // 4. Assign gems by dominant aspect. Textures are always assigned so the shader layer arrays stay
        //    aligned; only cards with magical potential then show the signature along their edges, and
        //    common cards (all-zero signature) render without it.
        for (var i = 0; i < cardTemplate.GemSockets.Length; i++)
        {
            var element = (Element)i;
            var rawValue = signature[element];
            var intensity = Mathf.Abs(rawValue); // 0..1
            var isPos = rawValue >= 0;

            var gemVis = _gemVisuals.FirstOrDefault(gv => gv.Element == element);
            if (gemVis != null) SetGemVisuals(renderer, cardTemplate, gemVis, isPos, i, intensity);
        }

        cardTemplate.SetSignatureEdgesVisible(signature.HasMagicalPotential());

        renderer.NameLabel.Text = rarity.ToString();
        renderer.AttrLabel.Text = signature.ToDebugString();
    }

    private static void SetGemVisuals(
        CardShaderRenderer renderer,
        CardTemplate cardTemplate,
        GemVisual gemVis,
        bool isPos,
        int i,
        float intensity)
    {
        // Select textures based on sign
        var socketTex = gemVis.SocketTexture;
        var gemTex = isPos
            ? gemVis.PositiveGemTexture
            : gemVis.NegativeGemTexture;
        cardTemplate.GemSockets[i].Texture = socketTex;
        cardTemplate.Gems[i].Texture = gemTex;

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
