using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Tests.TestUtilities;
using Godot;
using IrregularMeshNs = CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

namespace CardCleaner.Tests.Features.Worldgen.IrregularMesh;

/// <summary>
/// Which sessions the irregular screen takes: it ignores a session it did not start, ends the session when the
/// generator gives no irregular mesh map, and starts once the session service resolves after it was asked to.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class IrregularWorldMapScreenSessionTest
{
    // Frames to wait for the map, which generates off the main thread, or for a session state.
    private const int FrameBudget = 300;

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

    [TestCase]
    [TestCategory("Unit")]
    public async Task ASessionTheScreenDidNotStartLeavesItEmpty()
    {
        _session.StartSession(
            new List<CardSignature>(MeshMapGeneratorFixture.TerrainCards()),
            new List<CardSignature>(Abilities),
            SmallGenerator);

        await WaitFor(() => _session.CurrentGeneratedMap != null);
        await ISceneRunner.SyncProcessFrame;

        AssertThat(_session.CurrentGeneratedMap).IsNotNull();
        AssertBool(_screen.IsInitialized).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task AGeneratorThatGivesNoMeshMapEndsTheSession()
    {
        var states = new List<SessionState>();
        _session.StateChanged += states.Add;

        _screen.Initialize(MeshMapGeneratorFixture.TerrainCards(), Abilities, new PlainMapGenerator());
        await WaitFor(() => states.Contains(SessionState.WaitingForCards));

        AssertThat(states).Contains(SessionState.GeneratingMap, SessionState.WaitingForCards);
        AssertThat(states).NotContains(SessionState.Exploring);
        AssertThat(_session.CurrentState).IsEqual(SessionState.WaitingForCards);
        AssertBool(_screen.IsInitialized).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task InitializeBeforeTheServiceResolvesStartsOnceItDoes()
    {
        TestServices.Reset();
        TestServices.Container.RegisterSingleton<ITileRegistry>(new TileRegistry());
        var screen = CreateScreen();
        AddNode(screen);
        await ISceneRunner.SyncProcessFrame;

        screen.Initialize(MeshMapGeneratorFixture.TerrainCards(), Abilities, SmallGenerator);
        TestServices.Container.RegisterSingleton<IGameSessionService>(_session);
        TestServices.ExecutePendingCallbacks();
        await WaitFor(() => screen.IsInitialized);

        AssertBool(screen.IsInitialized).IsTrue();
    }

    /// <summary>
    /// A generator whose map is a real irregular map seen only as a plain <see cref="IGeneratedMap"/>.
    /// </summary>
    private sealed class PlainMapGenerator : IMapGenerator
    {
        public async Task<IGeneratedMap> GenerateAsync(
            MapGenerationConfig config,
            IProgress<float>? progress = null,
            CancellationToken cancellationToken = default) =>
            new PlainMap(await SmallGenerator.GenerateAsync(config, progress, cancellationToken));
    }

    private sealed class PlainMap(IGeneratedMap inner) : IGeneratedMap
    {
        public IMapData GetMapData() => inner.GetMapData();
        public int EnemyCount => inner.EnemyCount;
        public bool RemoveEnemyAt(int cellId) => inner.RemoveEnemyAt(cellId);
        public Vector2 PlayerStartPosition => inner.PlayerStartPosition;
    }
}
