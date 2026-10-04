using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;
using IrregularMeshNs = CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

namespace CardCleaner.Tests.Features.Worldgen.IrregularMesh;

/// <summary>
/// Session lifecycle of IrregularWorldMapScreen: regenerating replaces the previous session,
/// and a request made before the node enters the tree keeps all of its arguments.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class IrregularWorldMapScreenSessionTest
{
    private const int Seed = 4242;

    private readonly List<IrregularMeshNs.IrregularWorldMapScreen> _screens = new();

    private IrregularMeshNs.IrregularWorldMapScreen CreateScreen()
    {
        var viewport = new SubViewport { Name = "Viewport" };
        var screen = new IrregularMeshNs.IrregularWorldMapScreen { MeshRings = 3, Viewport = viewport };
        screen.AddChild(viewport);
        _screens.Add(screen);
        return screen;
    }

    private static CardSignature[] TerrainCards() => new[]
    {
        new CardSignature(new[] { 1f, 1f, 0f, -1f, 0f, 0.5f, 0f, 0f }),
        new CardSignature(new[] { -1f, -1f, 1f, 1f, 0f, -0.5f, 0f, 0f })
    };

    private static string TerrainOf(IrregularMeshNs.IrregularMesh mesh) =>
        string.Join(",", mesh.Vertices.Select(v => $"{v.TerrainType}:{v.TileId}:{v.ForegroundTileId}:{v.Position}"));

    private static List<IrregularMeshNs.IrregularMeshExplorationController> ExplorationControllers(Node screen) =>
        screen.GetNode("Viewport").GetChildren().OfType<IrregularMeshNs.IrregularMeshExplorationController>().ToList();

    [TestCase]
    public async Task RegeneratingReplacesThePreviousExplorationController()
    {
        var screen = CreateScreen();
        AddNode(screen);
        await ISceneRunner.SyncProcessFrame;

        screen.GenerateMap(Seed);
        var firstController = screen.ExplorationController!;
        screen.GenerateMap(Seed + 1);
        await ISceneRunner.SyncProcessFrame;

        AssertBool(IsInstanceValid(firstController)).IsFalse();
        AssertThat(ExplorationControllers(screen).Count).IsEqual(1);
        AssertThat(screen.ExplorationController).IsEqual(ExplorationControllers(screen)[0]);
    }

    [TestCase]
    public async Task DeferredGenerateMapKeepsTerrainCards()
    {
        var reference = CreateScreen();
        AddNode(reference);
        await ISceneRunner.SyncProcessFrame;
        reference.GenerateMap(Seed, TerrainCards(), null);

        var seedOnly = CreateScreen();
        AddNode(seedOnly);
        await ISceneRunner.SyncProcessFrame;
        seedOnly.GenerateMap(Seed);

        var deferred = CreateScreen();
        deferred.GenerateMap(Seed, TerrainCards(), null);
        AddNode(deferred);
        await ISceneRunner.SyncProcessFrame;

        AssertThat(TerrainOf(reference.Mesh!)).IsNotEqual(TerrainOf(seedOnly.Mesh!));
        AssertThat(TerrainOf(deferred.Mesh!)).IsEqual(TerrainOf(reference.Mesh!));
    }

    [TestCase]
    public async Task DeferredGenerateMapWithWfcKeepsItsRules()
    {
        var adjacency = new Dictionary<string, HashSet<string>> { { "water", new HashSet<string> { "water" } } };
        var tileToTerrain = new Dictionary<string, int> { { "water", 0 } };

        var reference = CreateScreen();
        AddNode(reference);
        await ISceneRunner.SyncProcessFrame;
        reference.GenerateMapWithWfc(Seed, adjacency, tileToTerrain);

        var deferred = CreateScreen();
        deferred.GenerateMapWithWfc(Seed, adjacency, tileToTerrain);
        AddNode(deferred);
        await ISceneRunner.SyncProcessFrame;

        AssertThat(TerrainOf(deferred.Mesh!)).IsEqual(TerrainOf(reference.Mesh!));
    }

    private static bool IsInstanceValid(GodotObject obj) => GodotObject.IsInstanceValid(obj);
}
