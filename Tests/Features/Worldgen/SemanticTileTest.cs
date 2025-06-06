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

        Assertions.AssertBool(grassTile.CanConnectTo(grassTile, Direction.North)).IsTrue();
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

        Assertions.AssertBool(anyTile.CanConnectTo(grassTile, Direction.North)).IsTrue();
        Assertions.AssertBool(grassTile.CanConnectTo(anyTile, Direction.North)).IsTrue();
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
}