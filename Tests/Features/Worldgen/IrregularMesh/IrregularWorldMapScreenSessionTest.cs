using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
    private const int FreeFrameBudget = 5;

    private static readonly IrregularMeshNs.MeshMapGenerator Generator = MeshMapGeneratorFixture.Create();

    private readonly List<IrregularMeshNs.IrregularWorldMapScreen> _screens = new();

    private IrregularMeshNs.IrregularWorldMapScreen CreateScreen()
    {
        var viewport = new SubViewport { Name = "Viewport" };
        var screen = new IrregularMeshNs.IrregularWorldMapScreen { MeshRings = 3, Viewport = viewport };
        screen.AddChild(viewport);
        _screens.Add(screen);
        return screen;
    }

    private static List<IrregularMeshNs.IrregularMeshExplorationController> ExplorationControllers(Node screen) =>
        screen.GetNode("Viewport").GetChildren().OfType<IrregularMeshNs.IrregularMeshExplorationController>().ToList();

    [TestCase]
    public async Task RegeneratingReplacesThePreviousExplorationController()
    {
        var screen = CreateScreen();
        AddNode(screen);
        await ISceneRunner.SyncProcessFrame;

        await screen.GenerateMap(Generator, Seed);
        var firstController = screen.ExplorationController!;
        await screen.GenerateMap(Generator, Seed + 1);
        // The map arrives in a continuation that can run before this frame's process step, so the queued free of the
        // first controller can take one more frame.
        for (var frame = 0; frame < FreeFrameBudget && IsInstanceValid(firstController); frame++)
            await ISceneRunner.SyncProcessFrame;

        AssertBool(IsInstanceValid(firstController)).IsFalse();
        AssertThat(ExplorationControllers(screen).Count).IsEqual(1);
        AssertThat(screen.ExplorationController).IsEqual(ExplorationControllers(screen)[0]);
    }

    [TestCase]
    public async Task StartingANewGenerationClosesTheRunningSession()
    {
        var screen = CreateScreen();
        AddNode(screen);
        await ISceneRunner.SyncProcessFrame;
        await screen.GenerateMap(Generator, Seed);
        var firstController = screen.ExplorationController!;

        // This map never arrives, so only the start of the new generation can have closed the first session.
        _ = screen.GenerateMap(MeshMapGeneratorFixture.Pending(), Seed + 1);

        AssertThat(screen.ExplorationController).IsNull();
        AssertBool(screen.IsInitialized).IsFalse();
        AssertBool(firstController.IsQueuedForDeletion()).IsTrue();
    }

    [TestCase]
    public async Task DeferredGenerateMapKeepsTerrainCards()
    {
        var reference = CreateScreen();
        AddNode(reference);
        await ISceneRunner.SyncProcessFrame;
        await reference.GenerateMap(Generator, Seed, MeshMapGeneratorFixture.TerrainCards(), null);

        var seedOnly = CreateScreen();
        AddNode(seedOnly);
        await ISceneRunner.SyncProcessFrame;
        await seedOnly.GenerateMap(Generator, Seed);

        var deferred = CreateScreen();
        var deferredGeneration = deferred.GenerateMap(Generator, Seed, MeshMapGeneratorFixture.TerrainCards(), null);
        AddNode(deferred);
        await deferredGeneration;

        var referenceTerrain = MeshMapGeneratorFixture.TerrainOf(reference.Mesh!);
        AssertThat(referenceTerrain).IsNotEqual(MeshMapGeneratorFixture.TerrainOf(seedOnly.Mesh!));
        AssertThat(MeshMapGeneratorFixture.TerrainOf(deferred.Mesh!)).IsEqual(referenceTerrain);
    }

    private static bool IsInstanceValid(GodotObject obj) => GodotObject.IsInstanceValid(obj);
}
