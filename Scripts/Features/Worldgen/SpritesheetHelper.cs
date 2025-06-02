using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen;

public static class SpriteSheetHelper
{
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
                var index = y * gridSize.X + x;
                var name = names?.ElementAtOrDefault(index) ?? $"tile_{index}";
                    
                regions.Add(new SpriteRegion(texture, rect, name));
            }
        }
            
        return regions;
    }
        
    // Create regions from an atlas with specific tile positions
    public static List<SpriteRegion> CreateRegionsFromAtlas(Texture2D texture, 
        Dictionary<string, Rect2I> namedRegions)
    {
        var regions = new List<SpriteRegion>();
            
        foreach (var kvp in namedRegions)
        {
            regions.Add(new SpriteRegion(texture, kvp.Value, kvp.Key));
        }
            
        return regions;
    }
}