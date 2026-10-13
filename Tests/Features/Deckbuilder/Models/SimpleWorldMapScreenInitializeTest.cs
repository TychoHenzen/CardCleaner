using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services;

namespace CardCleaner.Tests.Features.Deckbuilder.Models;

/// <summary>
/// SimpleWorldMapScreen keeps an Initialize request made before the node is ready and starts it once ready, generating
/// with the generator it was handed.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class SimpleWorldMapScreenInitializeTest
{
    private static readonly float[] SeedValues = { 0.5f, -0.3f, 0.8f, 0f, 0f, 0f, 0f, 0f };

    private readonly List<SessionState> _states = new();
    private GameSessionService _session = null!;
    private RecordingGenerator _generator = null!;

    [BeforeTest]
    public void Setup()
    {
        ServiceLocator.ResetForTesting();
        _states.Clear();
        _generator = new RecordingGenerator();
        _session = new GameSessionService();
        _session.StateChanged += _states.Add;
        AddNode(_session);
        ServiceLocator.Container.RegisterSingleton<IGameSessionService>(_session);
    }

    [AfterTest]
    public void TearDown()
    {
        _session.StateChanged -= _states.Add;
        ServiceLocator.ResetForTesting();
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
