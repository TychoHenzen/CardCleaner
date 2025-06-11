using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen;

public class GradientInfluenceComponent
{
    private readonly BaselineGradient _gradient;
    private readonly float _influence;

    public GradientInfluenceComponent(BaselineGradient gradient, float influence = 0.5f)
    {
        _gradient = gradient;
        _influence = influence;
    }

    public List<float> AdjustTileWeights(List<SemanticTile> tiles, Vector3I position, Vector3I mapSize)
    {
        if (_gradient == null) 
            return tiles.Select(t => t.BaseWeight).ToList();

        // Map 3D position to 2D gradient coordinates (use X,Y, ignore Z)
        var gradientPosition = new Vector2I(position.X, position.Y);
        var gradientMapSize = new Vector2I(mapSize.X, mapSize.Y);
        var gradientSignature = _gradient.GetSignatureAt(gradientPosition, gradientMapSize);
        
        var adjustedWeights = new List<float>();
        foreach (var tile in tiles)
        {
            var baseWeight = tile.BaseWeight;
            
            if (tile.Signature != null)
            {
                // Calculate similarity bonus
                var distance = tile.Signature.DistanceTo(gradientSignature);
                var similarity = 1f - (distance / 4f); // Normalize 0-1
                var signatureBonus = 1f + similarity * _influence;
                baseWeight *= signatureBonus;
            }
            
            adjustedWeights.Add(baseWeight);
        }

        return adjustedWeights;
    }
}