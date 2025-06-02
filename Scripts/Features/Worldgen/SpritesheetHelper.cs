using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Data;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen;

public static class SpriteSheetHelper
{
    // Create a LayerData from texture and rect
    public static LayerData CreateLayerData(Texture2D texture, Rect2I rect)
    {
        if (texture == null) return null;
        
        return new LayerData
        {
            Texture = texture,
            Region = new Vector4(
                (float)rect.Position.X / texture.GetWidth(),
                (float)rect.Position.Y / texture.GetHeight(),
                (float)rect.Size.X / texture.GetWidth(),
                (float)rect.Size.Y / texture.GetHeight()
            )
        };
    }
    
    // Create sprite regions from a regular grid spritesheet
    public static List<SpriteRegion> CreateRegionsFromGrid(Texture2D texture, Vector2I tileSize, 
        Vector2I gridSize, List<string> names = null)
    {
        var regions = new List<SpriteRegion>();
            
        for (int y = 0; y < gridSize.Y; y++)
        {
            for (int x = 0; x < gridSize.X; x++)
            {
                var rect = new Rect2I(x * tileSize.X, y * tileSize.Y, tileSize.X, tileSize.Y);
                regions.Add(new SpriteRegion(texture, rect));
            }
        }
            
        return regions;
    }
        
    // Create regions from an atlas with specific tile positions
    public static List<SpriteRegion> CreateRegionsFromAtlas(Texture2D texture, 
        Dictionary<string, Rect2I> namedRegions)
    {
        return namedRegions.Select(kvp => new SpriteRegion(texture, kvp.Value)).ToList();
    }
}