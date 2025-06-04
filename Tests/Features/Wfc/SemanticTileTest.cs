using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Core.Enum;
using CardCleaner.Scripts.Features.Worldgen;
using GdUnit4;

namespace CardCleaner.Tests.Features;

[TestSuite]
public class SemanticTileTest
{
    [TestCase]
    public void TestSocketCompatibility()
    {
        var grassTile = new SemanticTile
        {
            North = SocketType.Grasslands,
            East = SocketType.Grasslands,
            South = SocketType.Grasslands,
            West = SocketType.Grasslands
        };
        
        var stoneTile = new SemanticTile
        {
            North = SocketType.Mountains,
            East = SocketType.Mountains, 
            South = SocketType.Mountains,
            West = SocketType.Mountains
        };

        // Same socket types should connect
        Assertions.AssertBool(grassTile.CanConnectTo(grassTile, Direction.North)).IsTrue();
        
        // Different socket types should not connect
        Assertions.AssertBool(grassTile.CanConnectTo(stoneTile, Direction.North)).IsFalse();
    }

    [TestCase]
    public void TestAnySocketCompatibility()
    {
        var anyTile = new SemanticTile
        {
            North = SocketType.Any,
            East = SocketType.Any,
            South = SocketType.Any,
            West = SocketType.Any
        };
        
        var grassTile = new SemanticTile
        {
            North = SocketType.Grasslands,
            East = SocketType.Grasslands,
            South = SocketType.Grasslands,
            West = SocketType.Grasslands
        };

        // Any socket should connect to everything
        Assertions.AssertBool(anyTile.CanConnectTo(grassTile, Direction.North)).IsTrue();
        Assertions.AssertBool(grassTile.CanConnectTo(anyTile, Direction.North)).IsTrue();
    }

    [TestCase]
    public void TestDirectionalSocketMapping()
    {
        var tile = new SemanticTile
        {
            North = SocketType.Grasslands,
            East = SocketType.Mountains,
            South = SocketType.Swamp,
            West = SocketType.Any
        };

        var adjacentTile = new SemanticTile
        {
            North = SocketType.Swamp,  // Connects to our south
            East = SocketType.Any,     // Connects to our west
            South = SocketType.Grasslands,  // Connects to our north
            West = SocketType.Mountains    // Connects to our east
        };

        // Test all directions
        Assertions.AssertBool(tile.CanConnectTo(adjacentTile, Direction.North)).IsTrue();  // Grass -> Water (south of adjacent)
        Assertions.AssertBool(tile.CanConnectTo(adjacentTile, Direction.East)).IsTrue();   // Stone -> Stone (west of adjacent)
        Assertions.AssertBool(tile.CanConnectTo(adjacentTile, Direction.South)).IsTrue();  // Water -> Grass (north of adjacent)
        Assertions.AssertBool(tile.CanConnectTo(adjacentTile, Direction.West)).IsTrue();   // Any -> Any (east of adjacent)
    }

    [TestCase]
    public void TestNullTileConnection()
    {
        var tile = new SemanticTile
        {
            North = SocketType.Grasslands,
            East = SocketType.Grasslands,
            South = SocketType.Grasslands,
            West = SocketType.Grasslands
        };

        Assertions.AssertBool(tile.CanConnectTo(null, Direction.North)).IsFalse();
    }

    [TestCase]
    public void TestAnimationProperties()
    {
        var animatedTile = new SemanticTile
        {
            SpriteRegion = new SpriteRegion {Layers = new []{new TileReference(), new TileReference()}},
            FrameDuration = 0.5f
        };

        var staticTile = new SemanticTile
        {
            SpriteRegion = new SpriteRegion {Layers = new []{new TileReference()}},
            FrameDuration = 0.0f
        };

        Assertions.AssertBool(animatedTile.IsAnimated).IsTrue();
        Assertions.AssertBool(staticTile.IsAnimated).IsFalse();
    }
}