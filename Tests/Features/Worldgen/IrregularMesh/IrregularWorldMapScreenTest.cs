using System.Threading.Tasks;
using Godot;
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
    private static readonly IrregularMeshNs.MeshMapGenerator Generator = MeshMapGeneratorFixture.Create();

    private IrregularMeshNs.IrregularWorldMapScreen _screen = null!;

    private static IrregularMeshNs.IrregularWorldMapScreen CreateScreen(bool fogOfWarEnabled = true)
    {
        var viewport = new SubViewport { Name = "Viewport" };
        var screen = new IrregularMeshNs.IrregularWorldMapScreen
        {
            MeshRings = 2,
            VisionRange = 3f,
            FogOfWarEnabled = fogOfWarEnabled,
            ShowDebug = false,
            Viewport = viewport
        };
        screen.AddChild(viewport);
        return screen;
    }

    [BeforeTest]
    public async Task Setup()
    {
        _screen = CreateScreen();
        AddNode(_screen);
        await ISceneRunner.SyncProcessFrame;
    }

    // No Teardown needed - gdUnit4's AddNode handles cleanup automatically

    #region Initialization Tests

    [TestCase]
    public static void TestScreenStartsUninitialized()
    {
        // Create a fresh screen not in tree to test initial state
        var freshScreen = new IrregularMeshNs.IrregularWorldMapScreen();
        AssertBool(freshScreen.IsInitialized).IsFalse();
        AssertThat(freshScreen.Mesh).IsNull();
        AssertThat(freshScreen.MapData).IsNull();
        AddNode(freshScreen); // Let gdUnit4 handle cleanup
    }

    [TestCase]
    public static void TestDefaultPropertyValues()
    {
        var screen = new IrregularMeshNs.IrregularWorldMapScreen();
        AddNode(screen); // Let gdUnit4 handle cleanup

        AssertThat(screen.MeshRings).IsEqual(5);
        AssertThat(screen.VisionRange).IsEqual(3f);
        AssertBool(screen.FogOfWarEnabled).IsTrue();
        AssertBool(screen.ShowDebug).IsFalse();
    }

    #endregion

    #region Map Generation Tests

    [TestCase]
    public async Task TestGenerateMapCreatesMesh()
    {
        await _screen.GenerateMap(Generator, 12345);

        AssertBool(_screen.IsInitialized).IsTrue();
        AssertThat(_screen.Mesh).IsNotNull();
        AssertThat(_screen.MapData).IsNotNull();
    }

    [TestCase]
    public async Task TestGenerateMapCreatesFogOfWar()
    {
        await _screen.GenerateMap(Generator, 12345);

        AssertThat(_screen.FogOfWar).IsNotNull();
    }

    [TestCase]
    public async Task TestGenerateMapCreatesExplorationController()
    {
        await _screen.GenerateMap(Generator, 12345);

        AssertThat(_screen.ExplorationController).IsNotNull();
    }

    [TestCase]
    public static async Task TestGenerateMapWithSameSeedProducesSameMesh()
    {
        var screen1 = CreateScreen();
        var screen2 = CreateScreen();

        AddNode(screen1);
        AddNode(screen2);
        await ISceneRunner.SyncProcessFrame;

        await screen1.GenerateMap(Generator, 42);
        await screen2.GenerateMap(Generator, 42);

        AssertThat(screen1.Mesh!.Vertices.Count).IsEqual(screen2.Mesh!.Vertices.Count);
        AssertThat(screen1.Mesh!.Quads.Count).IsEqual(screen2.Mesh!.Quads.Count);
    }

    [TestCase]
    public async Task TestGenerateMapRaisesMapGeneratedEvent()
    {
        var eventRaised = false;
        _screen.MapGenerated += () => eventRaised = true;

        await _screen.GenerateMap(Generator, 12345);

        AssertBool(eventRaised).IsTrue();
    }

    #endregion

    #region Fog of War Tests

    [TestCase]
    public static async Task TestFogOfWarCanBeDisabled()
    {
        var screen = CreateScreen(fogOfWarEnabled: false);

        AddNode(screen);
        await ISceneRunner.SyncProcessFrame;

        await screen.GenerateMap(Generator, 12345);

        AssertThat(screen.FogOfWar).IsNull();
    }

    [TestCase]
    public async Task TestRevealAllUpdatesAllCells()
    {
        await _screen.GenerateMap(Generator, 12345);
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
    public async Task TestStartExplorationDoesNotThrow()
    {
        await _screen.GenerateMap(Generator, 12345);

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
    public async Task TestResetClearsMap()
    {
        await _screen.GenerateMap(Generator, 12345);
        _screen.Reset();

        AssertBool(_screen.IsInitialized).IsFalse();
        AssertThat(_screen.Mesh).IsNull();
        AssertThat(_screen.MapData).IsNull();
    }

    [TestCase]
    public async Task TestCanRegenerateAfterReset()
    {
        await _screen.GenerateMap(Generator, 12345);
        _screen.Reset();
        await _screen.GenerateMap(Generator, 54321);

        AssertBool(_screen.IsInitialized).IsTrue();
        AssertThat(_screen.Mesh).IsNotNull();
    }

    #endregion
}
