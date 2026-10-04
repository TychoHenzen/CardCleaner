using System.Collections.Generic;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services;

namespace CardCleaner.Tests.Features.Deckbuilder.Models;

/// <summary>
/// SimpleWorldMapScreen keeps an Initialize request made before the node is ready and starts it once ready.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class SimpleWorldMapScreenInitializeTest
{
    private static readonly float[] SeedValues = { 0.5f, -0.3f, 0.8f, 0f, 0f, 0f, 0f, 0f };

    private readonly List<SessionState> _states = new();
    private GameSessionService _session = null!;

    [BeforeTest]
    public void Setup()
    {
        ServiceLocator.ResetForTesting();
        _states.Clear();
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

        screen.Initialize(new[] { new CardSignature(SeedValues) }, new[] { new CardSignature() });
        AddNode(screen);
        await ISceneRunner.SyncProcessFrame;

        AssertBool(_states.Contains(SessionState.GeneratingMap)).IsTrue();
    }

    [TestCase]
    public async Task InitializeAfterReadyStartsTheSession()
    {
        var screen = new SimpleWorldMapScreen();
        AddNode(screen);
        await ISceneRunner.SyncProcessFrame;

        screen.Initialize(new[] { new CardSignature(SeedValues) }, new[] { new CardSignature() });

        AssertBool(_states.Contains(SessionState.GeneratingMap)).IsTrue();
    }
}
