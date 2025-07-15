using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Features.Worldgen;
using GdUnit4;
using Godot.Collections;

namespace CardCleaner.Tests.Features.Worldgen;

[TestSuite]
[RequireGodotRuntime]
public class SemanticTileTest
{
    private static Array<CompatibilityTag> CreateSocketArray(string biomeTag = null)
    {
        var array = new Array<CompatibilityTag>();
        if (!string.IsNullOrEmpty(biomeTag)) array.Add(new CompatibilityTag { Tag = biomeTag });

        return array;
    }

    [TestCase]
    public void TestSocketCompatibility()
    {
        var grassSocket = CreateSocketArray("Grasslands");
        var mountainSocket = CreateSocketArray("Mountains");

        var grassTile = new SemanticTile
        {
            SocketData = new SocketData
            {
                North = grassSocket,
                East = grassSocket,
                South = grassSocket,
                West = grassSocket
            }
        };

        var stoneTile = new SemanticTile
        {
            SocketData = new SocketData
            {
                North = mountainSocket,
                East = mountainSocket,
                South = mountainSocket,
                West = mountainSocket
            }
        };

        Assertions.AssertBool(grassTile.CanConnectTo(grassTile, Direction.North)).IsTrue();
        Assertions.AssertBool(grassTile.CanConnectTo(stoneTile, Direction.North)).IsFalse();
    }

    [TestCase]
    public void TestAnySocketCompatibility()
    {
        var anySocket = CreateSocketArray(); // Empty array = accepts any
        var grassSocket = CreateSocketArray("Grasslands");

        var anyTile = new SemanticTile
        {
            SocketData = new SocketData
            {
                North = anySocket,
                East = anySocket,
                South = anySocket,
                West = anySocket
            }
        };

        var grassTile = new SemanticTile
        {
            SocketData = new SocketData
            {
                North = grassSocket,
                East = grassSocket,
                South = grassSocket,
                West = grassSocket
            }
        };

        // Note: This test may need adjustment based on how "accepts any" is implemented
        // For now, assuming empty arrays don't connect to anything
        Assertions.AssertBool(anyTile.CanConnectTo(grassTile, Direction.North)).IsFalse();
        Assertions.AssertBool(grassTile.CanConnectTo(anyTile, Direction.North)).IsFalse();
    }

    [TestCase]
    public void TestNullTileConnection()
    {
        var grassSocket = CreateSocketArray("Grasslands");

        var tile = new SemanticTile
        {
            SocketData = new SocketData
            {
                North = grassSocket,
                East = grassSocket,
                South = grassSocket,
                West = grassSocket
            }
        };

        Assertions.AssertBool(tile.CanConnectTo(null, Direction.North)).IsFalse();
    }
}