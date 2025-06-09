using CardCleaner.Scripts.Core.Enum;
using CardCleaner.Scripts.Features.Worldgen;
using GdUnit4;

namespace CardCleaner.Tests.Features;

[TestSuite]
[RequireGodotRuntime]
public class SemanticTileTest
{
    [TestCase]
    public void TestSocketCompatibility()
    {
        var grassTile = new SemanticTile
        {
            North = new SocketDescriptor(){BiomeType = SocketType.Grasslands},
            East = new SocketDescriptor(){BiomeType = SocketType.Grasslands},
            South = new SocketDescriptor(){BiomeType = SocketType.Grasslands},
            West = new SocketDescriptor(){BiomeType = SocketType.Grasslands}
        };
        
        var stoneTile = new SemanticTile
        {
            North = new SocketDescriptor(){BiomeType = SocketType.Mountains},
            East = new SocketDescriptor(){BiomeType = SocketType.Mountains}, 
            South = new SocketDescriptor(){BiomeType = SocketType.Mountains},
            West = new SocketDescriptor(){BiomeType = SocketType.Mountains}
        };

        Assertions.AssertBool(grassTile.CanConnectTo(grassTile, Direction.North)).IsTrue();
        Assertions.AssertBool(grassTile.CanConnectTo(stoneTile, Direction.North)).IsFalse();
    }

    [TestCase]
    public void TestAnySocketCompatibility()
    {
        var anyTile = new SemanticTile
        {
            North = new SocketDescriptor(){AcceptsAny = true},
            East = new SocketDescriptor(){AcceptsAny = true},
            South = new SocketDescriptor(){AcceptsAny = true},
            West = new SocketDescriptor(){AcceptsAny = true}
        };
        
        var grassTile = new SemanticTile
        {
            North = new SocketDescriptor(){BiomeType = SocketType.Grasslands},
            East = new SocketDescriptor(){BiomeType = SocketType.Grasslands},
            South = new SocketDescriptor(){BiomeType = SocketType.Grasslands},
            West = new SocketDescriptor(){BiomeType = SocketType.Grasslands}
        };

        Assertions.AssertBool(anyTile.CanConnectTo(grassTile, Direction.North)).IsTrue();
        Assertions.AssertBool(grassTile.CanConnectTo(anyTile, Direction.North)).IsTrue();
    }

    [TestCase]
    public void TestNullTileConnection()
    {
        var tile = new SemanticTile
        {
            North = new SocketDescriptor(){BiomeType = SocketType.Grasslands},
            East = new SocketDescriptor(){BiomeType = SocketType.Grasslands},
            South = new SocketDescriptor(){BiomeType = SocketType.Grasslands},
            West = new SocketDescriptor(){BiomeType = SocketType.Grasslands}
        };

        Assertions.AssertBool(tile.CanConnectTo(null, Direction.North)).IsFalse();
    }
}