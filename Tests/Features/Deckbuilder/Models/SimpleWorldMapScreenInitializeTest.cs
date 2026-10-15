using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Tests.Features.Worldgen.IrregularMesh;
using CardCleaner.Tests.TestUtilities;

namespace CardCleaner.Tests.Features.Deckbuilder.Models;

/// <summary>
/// SimpleWorldMapScreen keeps an Initialize request made before the node is ready and starts it once ready, generating
/// with the generator it was handed. A screen that left the tree ignores a session service that resolves late, and a
/// generator that gives no grid map ends the session.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class SimpleWorldMapScreenInitializeTest
{
    // Frames to wait for the map, which generates off the main thread.
    private const int FrameBudget = 300;

    private static readonly float[] SeedValues = { 0.5f, -0.3f, 0.8f, 0f, 0f, 0f, 0f, 0f };
    private static readonly IMapGenerator MeshGenerator = MeshMapGeneratorFixture.Create(2);

    private readonly List<SessionState> _states = new();
    private GameSessionService _session = null!;
    private RecordingGenerator _generator = null!;

    [BeforeTest]
    public void Setup()
    {
        TestServices.Reset();
        _states.Clear();
        _generator = new RecordingGenerator();
        _session = new GameSessionService();
        _session.StateChanged += _states.Add;
        AddNode(_session);
        TestServices.Container.RegisterSingleton<IGameSessionService>(_session);
    }

    [AfterTest]
    public void TearDown()
    {
        _session.StateChanged -= _states.Add;
        TestServices.Reset();
    }

    [TestCase]
    public async Task InitializeBeforeReadyStartsTheSessionOnceReady()
    {
        var screen = new SimpleWorldMapScreen();

        screen.Initialize(new[] { new CardSignature(SeedValues) }, new[] { new CardSignature() }, _generator);
        AddNode(screen);
        await ISceneRunner.SyncProcessFrame;

        AssertBool(_states.Contains(SessionState.GeneratingMap)).IsTrue();
        await ISceneRunner.SyncProcessFrame;
        AssertInt(_generator.Calls).IsEqual(1);
    }

    [TestCase]
    public async Task InitializeAfterReadyStartsTheSession()
    {
        var screen = new SimpleWorldMapScreen();
        AddNode(screen);
        await ISceneRunner.SyncProcessFrame;

        screen.Initialize(new[] { new CardSignature(SeedValues) }, new[] { new CardSignature() }, _generator);

        AssertBool(_states.Contains(SessionState.GeneratingMap)).IsTrue();
        await ISceneRunner.SyncProcessFrame;
        AssertInt(_generator.Calls).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task AScreenThatLeftTheTreeIgnoresALateSessionService()
    {
        TestServices.Reset();
        var screen = new SimpleWorldMapScreen();
        AddNode(screen);
        await ISceneRunner.SyncProcessFrame;

        screen.Initialize(new[] { new CardSignature(SeedValues) }, new[] { new CardSignature() }, _generator);
        screen.GetParent().RemoveChild(screen);
        TestServices.Container.RegisterSingleton<IGameSessionService>(_session);
        TestServices.ExecutePendingCallbacks();
        await ISceneRunner.SyncProcessFrame;

        AssertBool(_states.Contains(SessionState.GeneratingMap))
            .OverrideFailureMessage("The screen started its waiting session after it left the tree")
            .IsFalse();
        AssertInt(_generator.Calls)
            .OverrideFailureMessage("The screen asked for a map after it left the tree")
            .IsEqual(0);
        screen.QueueFree();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task AGeneratorThatGivesNoGridMapEndsTheSession()
    {
        var screen = new SimpleWorldMapScreen();
        AddNode(screen);
        await ISceneRunner.SyncProcessFrame;

        // The irregular generator gives a real map that is not a grid map.
        screen.Initialize(MeshMapGeneratorFixture.TerrainCards(), new[] { new CardSignature() }, MeshGenerator);
        for (var frame = 0; frame < FrameBudget && !_states.Contains(SessionState.WaitingForCards); frame++)
            await ISceneRunner.SyncProcessFrame;

        AssertThat(_states).Contains(SessionState.GeneratingMap, SessionState.WaitingForCards);
        AssertThat(_states).NotContains(SessionState.Exploring);
        AssertThat(_session.CurrentState).IsEqual(SessionState.WaitingForCards);
    }

    /// <summary>Counts the generations the session asks for; the map never arrives.</summary>
    private sealed class RecordingGenerator : IMapGenerator
    {
        public int Calls { get; private set; }

        public Task<IGeneratedMap> GenerateAsync(
            MapGenerationConfig config,
            IProgress<float>? progress = null,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return new TaskCompletionSource<IGeneratedMap>().Task;
        }
    }
}
