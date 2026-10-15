using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Tests.Features.Packs;
using CardCleaner.Tests.Mocking;
using CardCleaner.Tests.TestUtilities;
using Godot;
using IrregularMeshNs = CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

namespace CardCleaner.Tests.Features.Worldgen.IrregularMesh;

/// <summary>
/// The irregular screen as a view of a real <see cref="GameSessionService"/>: it shows the map of a session it
/// started, animates the session's moves from cell to cell, and resets. Which sessions it takes is in
/// <see cref="IrregularWorldMapScreenSessionTest"/>.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class IrregularWorldMapScreenTest
{
    // Frames to wait for the map, which generates off the main thread, or for a session state.
    private const int FrameBudget = 300;
    private const int MovesToWatch = 3;

    // Seconds to wait for the explorer's moves, combat included.
    private const double MoveBudgetSeconds = 30;

    private static readonly IrregularMeshNs.MeshMapGenerator SmallGenerator = MeshMapGeneratorFixture.Create(2);

    private static readonly CardSignature[] Abilities =
    {
        new(new[] { 0.5f, -0.3f, 0.8f, -0.1f, 0.2f, -0.7f, 0.9f, -0.4f })
    };

    private GameSessionService _session = null!;
    private IrregularMeshNs.IrregularWorldMapScreen _screen = null!;

    private static IrregularMeshNs.IrregularWorldMapScreen CreateScreen()
    {
        var viewport = new SubViewport { Name = "Viewport" };
        var screen = new IrregularMeshNs.IrregularWorldMapScreen { Viewport = viewport };
        screen.AddChild(viewport);
        return screen;
    }

    [BeforeTest]
    public async Task Setup()
    {
        TestServices.Reset();
        TestServices.Container.RegisterSingleton<ITileRegistry>(new TileRegistry());
        _session = new GameSessionService();
        TestServices.Container.RegisterSingleton<IGameSessionService>(_session);
        AddNode(_session);
        _screen = CreateScreen();
        AddNode(_screen);
        await ISceneRunner.SyncProcessFrame;
    }

    [AfterTest]
    public static void Teardown() => TestServices.Reset();

    private static async Task WaitFor(Func<bool> condition)
    {
        for (var frame = 0; frame < FrameBudget && !condition(); frame++)
            await ISceneRunner.SyncProcessFrame;
    }

    private async Task ShowMap()
    {
        _screen.Initialize(MeshMapGeneratorFixture.TerrainCards(), Abilities, SmallGenerator);
        await WaitFor(() => _screen.IsInitialized);
        AssertBool(_screen.IsInitialized)
            .OverrideFailureMessage($"The irregular map did not show within {FrameBudget} frames")
            .IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TestScreenStartsUninitialized()
    {
        var freshScreen = AutoFree(new IrregularMeshNs.IrregularWorldMapScreen())!;
        AssertBool(freshScreen.IsInitialized).IsFalse();
        AssertThat(freshScreen.Mesh).IsNull();
        AssertThat(freshScreen.MapData).IsNull();
        AssertBool(freshScreen.FogOfWarEnabled).IsTrue();
        AssertBool(freshScreen.ShowDebug).IsFalse();
        AssertBool(freshScreen.UseRaycastVisibility).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void LootGeneratedSpawnsCardsInARowAtTheLootPoint()
    {
        var lootPoint = new Marker3D { Name = "LootSpawnPoint", Position = new Vector3(2f, 3f, 4f) };
        var cardSpawner = new MockCardSpawner { Name = "CardSpawner" };
        _screen.AddChild(lootPoint);
        _screen.AddChild(cardSpawner);
        _screen.LootSpawnPoint = lootPoint;

        var spawning = new RecordingCardSpawner();
        TestServices.Container.RegisterSingleton<ICardSpawningService>(spawning);
        TestServices.Container.RegisterSingleton<ICardSpawner>(cardSpawner);
        var loot = new List<CardSignature>
        {
            new(new[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f, 0.8f }),
            new(new[] { -0.1f, -0.2f, -0.3f, -0.4f, -0.5f, -0.6f, -0.7f, -0.8f }),
            new(new[] { 0.9f, 0.8f, 0.7f, 0.6f, 0.5f, 0.4f, 0.3f, 0.2f })
        };

        ((ISessionView)_screen).OnLootGenerated(loot);

        AssertThat(spawning.Spawned.Count).IsEqual(loot.Count);
        for (var cardIndex = 0; cardIndex < loot.Count; cardIndex++)
        {
            AssertThat(spawning.Spawned[cardIndex].Signature.Elements).IsEqual(loot[cardIndex].Elements);
            AssertThat(spawning.Spawned[cardIndex].Transform.Origin)
                .IsEqual(new Vector3(2f + cardIndex * 0.5f, 3f, 4f));
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task ASessionTheScreenStartsShowsItsMap()
    {
        var mapsShown = 0;
        _screen.MapGenerated += () => mapsShown++;

        await ShowMap();

        AssertThat(_screen.Mesh).IsNotNull();
        AssertThat(_screen.MapData).IsNotNull();
        AssertThat(_screen.FogOfWar).IsNotNull();
        AssertInt(mapsShown).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task TheScreenAnimatesEachMoveToANeighbouringCell()
    {
        var cells = new List<int>();
        var movesReported = 0;
        var animatedArrivals = 0;
        var movesNotAnimated = 0;
        _screen.MapGenerated += () => FindMovement(_screen).MovementCompleted += (_, _) => animatedArrivals++;
        _screen.PlayerMoved += (cellId, _) => cells.Add(cellId);
        // The screen subscribed to the session first, so this runs after the screen took the move. An animated move
        // is not shown yet, and every earlier move arrived through its own animation before the session sent this one;
        // a teleport shows the move at once and raises no animated arrival.
        _session.PlayerMovedWorld += _ =>
        {
            movesReported++;
            if (cells.Count >= movesReported || animatedArrivals < movesReported - 1)
                movesNotAnimated++;
        };
        await ShowMap();
        var previous = _screen.MapData!.PlayerStartCell!.Value;

        var clock = Stopwatch.StartNew();
        while (cells.Count < MovesToWatch && !SessionEnded() && clock.Elapsed.TotalSeconds < MoveBudgetSeconds)
            await ISceneRunner.SyncProcessFrame;

        AssertInt(cells.Count)
            .OverrideFailureMessage($"The explorer made {cells.Count} of {MovesToWatch} moves on the irregular map")
            .IsGreaterEqual(MovesToWatch);
        AssertInt(movesNotAnimated)
            .OverrideFailureMessage("A move was shown without its animation, or before the last one arrived")
            .IsEqual(0);
        foreach (var cell in cells)
        {
            AssertBool(cell == previous || _screen.MapData.GetAdjacentCells(previous).Contains(cell))
                .OverrideFailureMessage($"The player jumped from cell {previous} to cell {cell}")
                .IsTrue();
            previous = cell;
        }
    }

    private bool SessionEnded() =>
        _session.CurrentState is SessionState.GeneratingLoot or SessionState.SessionComplete
            or SessionState.WaitingForCards;

    private static IrregularMeshNs.IrregularMeshMovementController FindMovement(Node root) =>
        root.FindChildren("*", "", true, false).OfType<IrregularMeshNs.IrregularMeshMovementController>().Single();

    // Leaving the tree disconnects the screen from the session; entering it again must connect it again, or a start
    // made after that waits for a service that never comes.
    [TestCase]
    [TestCategory("Unit")]
    public async Task AScreenAddedBackToTheTreeConnectsToTheSessionAgain()
    {
        var parent = _screen.GetParent();
        parent.RemoveChild(_screen);
        await ISceneRunner.SyncProcessFrame;
        parent.AddChild(_screen);
        await ISceneRunner.SyncProcessFrame;

        await ShowMap();

        AssertBool(_screen.IsInitialized).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task RevealAllShowsEveryCell()
    {
        await ShowMap();

        _screen.RevealAll();

        for (var cellId = 0; cellId < _screen.MapData!.CellCount; cellId++)
            AssertBool(_screen.FogOfWar!.IsVisible(cellId)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task ToggleDebugSwitchesTheDebugRenderer()
    {
        var debugRenderer = _screen.Viewport!.GetNode<CanvasItem>("DebugRenderer");
        AssertBool(debugRenderer.Visible).IsFalse();

        _screen.ToggleDebug();

        AssertBool(_screen.ShowDebug).IsTrue();
        AssertBool(debugRenderer.Visible).IsTrue();
        await ISceneRunner.SyncProcessFrame;
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task ResetClearsTheMapAndStopsShowingTheSession()
    {
        await ShowMap();
        var moves = 0;
        _screen.PlayerMoved += (_, _) => moves++;

        _screen.Reset();
        for (var frame = 0; frame < 30; frame++)
            await ISceneRunner.SyncProcessFrame;

        AssertBool(_screen.IsInitialized).IsFalse();
        AssertThat(_screen.Mesh).IsNull();
        AssertThat(_screen.MapData).IsNull();
        AssertInt(moves).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task ANewSessionShowsAfterTheResetOfTheLastOne()
    {
        await ShowMap();
        _screen.Reset();
        _session.ResetSession();

        await ShowMap();

        AssertBool(_screen.IsInitialized).IsTrue();
    }
}
