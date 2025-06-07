using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Enum;
using Godot;
using Godot.Collections;

namespace CardCleaner.Scripts.Features.Worldgen;

public class GradientInfluencedWfcGenerator : SemanticWfcGenerator
{
    private readonly BaselineGradient _gradient;
    private readonly float _gradientInfluence;

    public GradientInfluencedWfcGenerator(Array<SemanticTile> tileSet, Vector2I mapSize, 
        ulong seed, BaselineGradient gradient, float gradientInfluence = 0.5f) 
        : base(tileSet, mapSize, seed)
    {
        _gradient = gradient;
        _gradientInfluence = gradientInfluence;
    }

    protected override SemanticTile ChooseWeightedTile(List<SemanticTile> tiles, Vector2I position)
    {
        if (_gradient == null) return base.ChooseWeightedTile(tiles, position);

        // Get gradient signature at this position
        var gradientSignature = _gradient.GetSignatureAt(position, MapSize);
        
        // Calculate signature-based weights
        var adjustedWeights = new List<float>();
        foreach (var tile in tiles)
        {
            var baseWeight = tile.BaseWeight;
            
            if (tile.Signature != null)
            {
                // Calculate similarity bonus
                var distance = tile.Signature.DistanceTo(gradientSignature);
                var similarity = 1f - (distance / 4f); // Normalize 0-1
                var signatureBonus = 1f + similarity * _gradientInfluence;
                baseWeight *= signatureBonus;
            }
            
            adjustedWeights.Add(baseWeight);
        }

        // Select using adjusted weights
        var totalWeight = adjustedWeights.Sum();
        if (totalWeight <= 0) return tiles[0];

        var randomValue = Rng.Randf() * totalWeight;
        var currentWeight = 0f;

        for (int i = 0; i < tiles.Count; i++)
        {
            currentWeight += adjustedWeights[i];
            if (randomValue <= currentWeight)
                return tiles[i];
        }

        return tiles[^1];
    }
}