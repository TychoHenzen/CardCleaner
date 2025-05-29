using System;
using System.Linq;
using Godot;
using CardCleaner.Scripts.Interfaces;

namespace CardCleaner.Scripts.Services
{
    /// <summary>
    /// Generates card visuals deterministically from a CardSignature.
    /// Picks base, border, corners, and banner textures based on signature-derived rarity.
    /// </summary>
    public class SignatureCardGenerator : ICardGenerator
    {
        private readonly CardSignature _signature;
        private readonly RandomNumberGenerator _rng;
        private readonly RarityVisual[] _rarityVisuals;
        private readonly BaseCardType[] _baseTypes;
        private readonly GemVisual[] _gemVisuals;

        public SignatureCardGenerator(
            CardSignature signature,
            RarityVisual[] rarityVisuals,
            BaseCardType[] baseTypes,
            GemVisual[] gemVisuals)
        {
            _signature = signature;
            _rng = new RandomNumberGenerator
            {
                Seed = (uint)ComputeSeed(signature)
            };
            _rarityVisuals = rarityVisuals;
            _baseTypes = baseTypes;
            _gemVisuals = gemVisuals;
        }


        public void Verify()
        {
            // No verification implemented yet
        }

        public void RandomizeCardRenderer(CardShaderRenderer renderer)
        {
            // 1. Determine rarity
            var rarity = DetermineRarity(_signature);
            GD.Print($"[SignatureGenerator] Rarity: {rarity}");

            // 2. Apply per‐rarity visuals
            var visuals = _rarityVisuals.FirstOrDefault(rv => rv.Rarity == rarity);
            if (visuals != null)
            {
                Apply(renderer.CardBase, visuals.BaseOptions);
                Apply(renderer.Border, visuals.BorderOptions);
                Apply(renderer.Corners, visuals.CornerOptions);
                Apply(renderer.Banner, visuals.BannerOptions);
            }

            // 3. Select matching BaseCardType
            var candidates = _baseTypes
                .Where(bt => bt.CanMatch(_signature))
                .ToArray();
            if (candidates.Length > 0)
            {
                var chosenBase = SelectWeighted(candidates, bt => bt.CalculateMatchWeight(_signature));
                Apply(renderer.Art, chosenBase.ArtOptions);
                Apply(renderer.Symbol, chosenBase.SymbolOptions);
            }

            // 4. Assign gems by dominant aspect
            for (int i = 0; i < renderer.GemSockets.Length; i++)
            {
                var element = (Element)i;
                var aspect = _signature.GetDominantAspect(element);
                var gemVis = _gemVisuals.FirstOrDefault(gv => gv.Aspect == aspect);
                if (gemVis != null)
                {
                    renderer.GemSockets[i].Texture = gemVis.SocketTexture;
                    renderer.Gems[i].Texture = gemVis.GemTexture;
                }
            }
        }


        private int ComputeSeed(CardSignature signature)
        {
            int seed = 17;
            foreach (var v in signature.Elements)
                seed = seed * 23 + Mathf.RoundToInt(v * 1000);
            return seed;
        }

        private CardRarity DetermineRarity(CardSignature signature)
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

        
        private void Apply(LayerData layer, Texture2D[] options)
        {
            if (options == null || options.Length == 0) return;
            var idx = options.Length == 1
                ? 0
                : _rng.RandiRange(0, options.Length - 1);
            layer.Texture = options[idx];
        }
        

        private T SelectWeighted<T>(T[] items, Func<T, float> weightFn)
        {
            var weights = items.Select(weightFn).ToArray();
            float total = weights.Sum();
            if (total <= 0f) return items[0];

            float pick = _rng.Randf() * total;
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
}