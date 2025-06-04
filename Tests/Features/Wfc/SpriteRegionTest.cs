using CardCleaner.Scripts.Features.Worldgen;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features;

[TestSuite]
public class SpriteRegionTest
{
    [TestCase]
    public void TestSpriteRegionCreation()
    {
        var region = new SpriteRegion();
        
        Assertions.AssertThat(region).IsNotNull();
        Assertions.AssertThat(region.Layers).IsNotNull();
    }

    [TestCase]
    public void TestSpriteRegionWithTileReference()
    {
        var tileRef = new CardCleaner.Scripts.Core.Data.TileReference
        {
            SourceId = 0,
            AtlasCoords = new Vector2I(1, 1)
        };
        
        var region = new SpriteRegion(new[] { tileRef });
        
        Assertions.AssertThat(region.Layers.Length).IsEqual(1);
        Assertions.AssertThat(region.Layers[0].AtlasCoords).IsEqual(new Vector2I(1, 1));
    }
}