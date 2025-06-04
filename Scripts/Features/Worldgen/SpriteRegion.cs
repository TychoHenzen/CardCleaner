using System;
using CardCleaner.Scripts.Core.Data;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen;

[Tool]
[GlobalClass]
public partial class SpriteRegion : Resource
{
    [Export] public TileReference[] Layers { get; set; } = Array.Empty<TileReference>();

    public SpriteRegion()
    {
        
    }
    public SpriteRegion(TileReference[]? tileReferences)
    {
        Layers = tileReferences ?? Array.Empty<TileReference>();
    }

    public SpriteRegion(TileSet tileSet, Vector2I atlasCoords)
    {
        Layers = new[]
        {
            new TileReference
            {
                AtlasCoords = atlasCoords,
            }
        };
    }

}