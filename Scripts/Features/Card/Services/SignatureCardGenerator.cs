using System.Linq;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Core.Randomness;
using Godot;
using CardCleaner.Scripts.Features.Card.Models.Effects;

namespace CardCleaner.Scripts.Features.Card.Services;

/// <summary>
///     Generates card visuals deterministically from a CardSignature.
///     Picks base, border, corners, and banner textures based on signature-derived rarity.
/// </summary>
/// 
[Service(typeof(ICardGenerator))]
internal sealed class SignatureCardGenerator : ICardGenerator
{
    private readonly BaseCardType[] _baseTypes;
    private readonly GemVisual[] _gemVisuals;
    private readonly RarityVisual[] _rarityVisuals;

    public SignatureCardGenerator(RarityVisual[] rarityVisuals, BaseCardType[] baseTypes, GemVisual[] gemVisuals)
    {
        _rarityVisuals = rarityVisuals;
        _baseTypes = baseTypes;
        _gemVisuals = gemVisuals;
    }

    public void GenerateCardRenderer(
        CardShaderRenderer renderer,
        CardSignature signature,
        int? lookSeed,
        CardTemplate cardTemplate)
    {
        // A provider whose exports are unset registers null arrays; the generator then leaves the card as it was.
        if (_rarityVisuals == null || _baseTypes == null || _gemVisuals == null)
            return;
        // A card with magical potential has no look seed; its look comes from its signature.
        var look = signature.HasMagicalPotential() ? null : lookSeed;
        // The look seed drives the seed and the art choice of a mundane card; rarity and gems stay with the signature.
        var rng = RandomSources.Create(look.HasValue
            ? unchecked((uint)look.Value)
            : (uint)SignatureCardHelper.ComputeSeed(signature));
        // 1. Determine rarity
        var rarity = SignatureCardHelper.DetermineRarity(new[] { signature });

        ApplyRarityVisuals(rng, cardTemplate, rarity);

        // The base type: by the look seed alone for a mundane card, otherwise by matching the signature.
        ApplyBaseType(rng, cardTemplate, SelectBaseType(rng, signature, look));

        AssignGems(renderer, cardTemplate, signature);

        cardTemplate.SetSignatureEdgesVisible(signature.HasMagicalPotential());
        cardTemplate.SetEnergyOrbVisible(signature.HasMagicalPotential());

        // Art-region effects from the rarity and the signature's intensity.
        SetArtEffects(renderer, signature, look, rarity, cardTemplate.Art.Texture);

        renderer.NameLabel.Text = rarity.ToString();
        renderer.AttrLabel.Text = signature.ToDebugString();
    }

    private void ApplyRarityVisuals(IRandom rng, CardTemplate cardTemplate, CardRarity rarity)
    {
        var visuals = _rarityVisuals.FirstOrDefault(rv => rv.Rarity == rarity);
        if (visuals == null) return;

        SignatureCardHelper.Apply(rng, cardTemplate.CardBase, visuals.BaseOptions);
        SignatureCardHelper.Apply(rng, cardTemplate.Border, visuals.BorderOptions);
        SignatureCardHelper.Apply(rng, cardTemplate.Corners, visuals.CornerOptions);
        SignatureCardHelper.Apply(rng, cardTemplate.Banner, visuals.BannerOptions);
        SignatureCardHelper.Apply(rng, cardTemplate.ImageBackground, visuals.ImageBackgroundOptions);
        SignatureCardHelper.Apply(rng, cardTemplate.DescriptionBox, visuals.DescriptionBoxOptions);
        SignatureCardHelper.Apply(rng, cardTemplate.EnergyContainer, visuals.EnergyContainerOptions);
    }

    private static void ApplyBaseType(IRandom rng, CardTemplate cardTemplate, BaseCardType? chosenBase)
    {
        if (chosenBase == null) return;

        SignatureCardHelper.Apply(rng, cardTemplate.Art, chosenBase.ArtOptions);
        SignatureCardHelper.Apply(rng, cardTemplate.Symbol, chosenBase.SymbolOptions);
        SignatureCardHelper.Apply(rng, cardTemplate.EnergyFill1, chosenBase.EnergyFill1Options);
        SignatureCardHelper.Apply(rng, cardTemplate.EnergyFill2, chosenBase.EnergyFill2Options);
    }

    // Gem textures are always assigned so the shader layer arrays stay aligned; only cards with magical
    // potential then show the signature along their edges, and common cards render without it.
    private void AssignGems(CardShaderRenderer renderer, CardTemplate cardTemplate, CardSignature signature)
    {
        for (var i = 0; i < cardTemplate.GemSockets.Length; i++)
        {
            var element = (Element)i;
            var rawValue = signature[element];

            var gemVis = _gemVisuals.FirstOrDefault(gv => gv.Element == element);
            if (gemVis != null) SetGemVisuals(renderer, cardTemplate, gemVis, i, rawValue);
        }
    }

    private BaseCardType? SelectBaseType(IRandom rng, CardSignature signature, int? look)
    {
        if (look.HasValue)
            return _baseTypes.Length == 0 ? null : SignatureCardHelper.PickUniform(rng, _baseTypes);

        var candidates = _baseTypes
            .Where(bt => bt.CanMatch(signature))
            .ToArray();
        return candidates.Length == 0
            ? null
            : SignatureCardHelper.SelectWeighted(rng, candidates, bt => bt.CalculateMatchWeight(signature));
    }

    /// <remarks>
    ///     Intensity is the mean element magnitude (<see cref="CardEffectMapping.IntensityOf" />), so the all-zero
    ///     signature of an ordinary pack card is Dormant and nearly every common card shows the worn scratches.
    ///     That is the decided mapping; the owner may revisit it.
    ///     <para>
    ///         The rarity effect waits for the art's bevel map, which is baked off the main thread the first time
    ///         that art is shown; until then the card shows its condition effect only.
    ///     </para>
    /// </remarks>
    private static void SetArtEffects(
        CardShaderRenderer renderer,
        CardSignature signature,
        int? look,
        CardRarity rarity,
        Texture2D? art)
    {
        var rarityEffect = CardEffectMapping.RarityEffectFor(rarity);
        var conditionEffect = CardEffectMapping.ConditionEffectFor(CardEffectMapping.IntensityOf(signature.Elements));
        var seed = look.HasValue ? CardEffectSeed.ForLook(look.Value) : CardEffectSeed.For(signature);

        // Only the rarity effects read the bevel map, so a card without one does not wait for it.
        if (rarityEffect == RarityEffect.None || art == null)
        {
            renderer.SetArtEffects(rarityEffect, conditionEffect, seed, null);
            return;
        }

        // The request number lets a map that arrives after a newer generation of this card be ignored.
        var request = renderer.SetArtEffects(RarityEffect.None, conditionEffect, seed, null);
        var rendererId = renderer.GetInstanceId();
        CardEffectNormalMapCache.Request(art, map =>
        {
            // The card may have been freed while its map was baking.
            if (GodotObject.InstanceFromId(rendererId) is CardShaderRenderer waiting)
                waiting.SetArtNormalMap(request, rarityEffect, map);
        });
    }

    private static void SetGemVisuals(
        CardShaderRenderer renderer,
        CardTemplate cardTemplate,
        GemVisual gemVis,
        int i,
        float rawValue)
    {
        var isPos = rawValue >= 0;
        var intensity = Mathf.Abs(rawValue); // 0..1

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
