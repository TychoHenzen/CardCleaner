using System.Collections.Generic;
using System.Threading.Tasks;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;
using IrregularMeshNs = CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

namespace CardCleaner.Tests.Features.Worldgen.IrregularMesh;

/// <summary>
/// Integration tests for IrregularWorldMapScreen.
/// Verifies complete system integration of mesh, rendering,
/// fog of war, pathfinding, and exploration.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class IrregularWorldMapScreenTest
{
    private IrregularMeshNs.IrregularWorldMapScreen _screen = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _screen = new IrregularMeshNs.IrregularWorldMapScreen
        {
            MeshRings = 2,
            WorldScale = 16f,
            VisionRange = 3f,
            FogOfWarEnabled = true,
            ShowDebug = false
        };
        AddNode(_screen);
        await ISceneRunner.SyncProcessFrame;
    }

    // No Teardown needed - gdUnit4's AddNode handles cleanup automatically

    #region Initialization Tests

    [TestCase]
    public void TestScreenStartsUninitialized()
    {
        // Create a fresh screen not in tree to test initial state
        var freshScreen = new IrregularMeshNs.IrregularWorldMapScreen();
        AssertBool(freshScreen.IsInitialized).IsFalse();
        AssertThat(freshScreen.Mesh).IsNull();
        AssertThat(freshScreen.MapData).IsNull();
        AddNode(freshScreen); // Let gdUnit4 handle cleanup
    }

    [TestCase]
    public void TestDefaultPropertyValues()
    {
        var screen = new IrregularMeshNs.IrregularWorldMapScreen();
        AddNode(screen); // Let gdUnit4 handle cleanup

        AssertThat(screen.MeshRings).IsEqual(5);
        AssertThat(screen.WorldScale).IsEqual(16f);
        AssertThat(screen.VisionRange).IsEqual(3f);
        AssertBool(screen.FogOfWarEnabled).IsTrue();
        AssertBool(screen.ShowDebug).IsFalse();
    }

    #endregion

    #region Map Generation Tests

    [TestCase]
    public void TestGenerateMapCreatesMesh()
    {
        _screen.GenerateMap(12345);

        AssertBool(_screen.IsInitialized).IsTrue();
        AssertThat(_screen.Mesh).IsNotNull();
        AssertThat(_screen.MapData).IsNotNull();
    }

    [TestCase]
    public void TestGenerateMapCreatesFogOfWar()
    {
        _screen.GenerateMap(12345);

        AssertThat(_screen.FogOfWar).IsNotNull();
    }

    [TestCase]
    public void TestGenerateMapCreatesExplorationController()
    {
        _screen.GenerateMap(12345);

        AssertThat(_screen.ExplorationController).IsNotNull();
    }

    [TestCase]
    public async Task TestGenerateMapWithSameSeedProducesSameMesh()
    {
        var screen1 = new IrregularMeshNs.IrregularWorldMapScreen { MeshRings = 2 };
        var screen2 = new IrregularMeshNs.IrregularWorldMapScreen { MeshRings = 2 };

        AddNode(screen1);
        AddNode(screen2);
        await ISceneRunner.SyncProcessFrame;

        screen1.GenerateMap(42);
        screen2.GenerateMap(42);

        AssertThat(screen1.Mesh!.Vertices.Count).IsEqual(screen2.Mesh!.Vertices.Count);
        AssertThat(screen1.Mesh!.Quads.Count).IsEqual(screen2.Mesh!.Quads.Count);
    }

    [TestCase]
    public void TestGenerateMapRaisesMapGeneratedEvent()
    {
        var eventRaised = false;
        _screen.MapGenerated += () => eventRaised = true;

        _screen.GenerateMap(12345);

        AssertBool(eventRaised).IsTrue();
    }

    [TestCase]
    public void TestGenerateMapWithWfcCreatesMesh()
    {
        var adjacencyRules = new Dictionary<string, HashSet<string>>
        {
            { "grass", new HashSet<string> { "grass", "sand" } },
            { "sand", new HashSet<string> { "grass", "sand", "water" } },
            { "water", new HashSet<string> { "sand", "water" } }
        };

        var tileToTerrain = new Dictionary<string, int>
        {
            { "grass", 1 },
            { "sand", 2 },
            { "water", 0 }
        };

        _screen.GenerateMapWithWfc(12345, adjacencyRules, tileToTerrain);

        AssertBool(_screen.IsInitialized).IsTrue();
        AssertThat(_screen.Mesh).IsNotNull();
    }

    #endregion

    #region Fog of War Tests

    [TestCase]
    public async Task TestFogOfWarCanBeDisabled()
    {
        var screen = new IrregularMeshNs.IrregularWorldMapScreen
        {
            MeshRings = 2,
            FogOfWarEnabled = false
        };

        AddNode(screen);
        await ISceneRunner.SyncProcessFrame;

        screen.GenerateMap(12345);

        AssertThat(screen.FogOfWar).IsNull();
    }

    [TestCase]
    public void TestRevealAllUpdatesAllCells()
    {
        _screen.GenerateMap(12345);
        _screen.RevealAll();

        // All cells should be seen
        var allSeen = true;
        for (int i = 0; i < _screen.MapData!.CellCount; i++)
        {
            if (!_screen.FogOfWar!.HasBeenSeen(i))
            {
                allSeen = false;
                break;
            }
        }

        AssertBool(allSeen).IsTrue();
    }

    #endregion

    #region Exploration Tests

    [TestCase]
    public void TestStartExplorationDoesNotThrow()
    {
        _screen.GenerateMap(12345);

        // Should not throw
        _screen.StartExploration();
        _screen.StopExploration();
    }

    #endregion

    #region Debug Visualization Tests

    [TestCase]
    public void TestToggleDebugSwitchesVisibility()
    {
        AssertBool(_screen.ShowDebug).IsFalse();

        _screen.ToggleDebug();

        AssertBool(_screen.ShowDebug).IsTrue();

        _screen.ToggleDebug();

        AssertBool(_screen.ShowDebug).IsFalse();
    }

    #endregion

    #region Reset Tests

    [TestCase]
    public void TestResetClearsMap()
    {
        _screen.GenerateMap(12345);
        _screen.Reset();

        AssertBool(_screen.IsInitialized).IsFalse();
        AssertThat(_screen.Mesh).IsNull();
        AssertThat(_screen.MapData).IsNull();
    }

    [TestCase]
    public void TestCanRegenerateAfterReset()
    {
        _screen.GenerateMap(12345);
        _screen.Reset();
        _screen.GenerateMap(54321);

        AssertBool(_screen.IsInitialized).IsTrue();
        AssertThat(_screen.Mesh).IsNotNull();
    }

    #endregion
}
