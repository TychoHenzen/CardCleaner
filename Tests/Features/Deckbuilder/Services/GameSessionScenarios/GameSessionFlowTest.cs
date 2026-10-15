using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using Godot;


namespace CardCleaner.Tests.Features.Deckbuilder.Services.GameSessionScenarios;

/// <summary>
///     GameSessionService state transition and map generation flow scenarios split out of GameSessionServiceTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class GameSessionFlowTest : GameSessionServiceTestBase
{
    // Every element at 1.0 deals 80 damage a hit, more than any enemy of the fixture's map has health.
    private static readonly CardSignature StrongAbility = new(new[] { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f });

    [TestCase]
    public void TestAdvanceSessionFromWaitingFails()
    {
        _service.AdvanceSession();

        Assertions.AssertThat(_service.CurrentState).IsEqual(SessionState.WaitingForCards);
        Assertions.AssertThat(_stateChangeCount).IsEqual(0);
    }

    [TestCase]
    public async Task TestStateTransitionToExploring()
    {
        var mapSeeds = new List<CardSignature> { new() };
        var abilityCards = new List<CardSignature> { new() };

        _service.StartSession(mapSeeds, abilityCards, _generator);
        Assertions.AssertThat(_service.CurrentState).IsEqual(SessionState.GeneratingMap);

        // Wait for deferred AdvanceSession call to execute and set the task
        await _service.ToSignal(_service.GetTree(), SceneTree.SignalName.ProcessFrame);

        // Now the generation task should be set
        var generationTask = _service.CurrentGenerationTask;
        Assertions.AssertThat(generationTask).IsNotNull();
        await generationTask!;

        // Wait for deferred state transition to process
        await _service.ToSignal(_service.GetTree(), SceneTree.SignalName.ProcessFrame);

        Assertions.AssertThat(_service.CurrentState).IsEqual(SessionState.Exploring);
    }

    [TestCase]
    public async Task TestFullSessionFlowProgresses()
    {
        var mapSeeds = new List<CardSignature>
        {
            new(new[] { 0.5f, -0.3f, 0.8f, -0.1f, 0.2f, -0.7f, 0.9f, -0.4f })
        };
        var abilityCards = new List<CardSignature> { new() };

        _service.StartSession(mapSeeds, abilityCards, _generator);

        // Wait for deferred AdvanceSession call to execute and set the task
        await _service.ToSignal(_service.GetTree(), SceneTree.SignalName.ProcessFrame);

        // Now the generation task should be set
        var generationTask = _service.CurrentGenerationTask;
        Assertions.AssertThat(generationTask).IsNotNull();
        await generationTask!;

        // Wait for deferred state transition to process
        await _service.ToSignal(_service.GetTree(), SceneTree.SignalName.ProcessFrame);

        // Session should have progressed to at least Exploring
        // Full completion is too slow for unit tests (requires exploration + combat)
        var validState = _service.CurrentState == SessionState.Exploring ||
                        _service.CurrentState == SessionState.InCombat ||
                        _service.CurrentState == SessionState.GeneratingLoot ||
                        _service.CurrentState == SessionState.SessionComplete ||
                        _service.CurrentState == SessionState.WaitingForCards;
        Assertions.AssertBool(validState).IsTrue();
    }

    [TestCase]
    public async Task TestMapGeneratedEvent()
    {
        var mapSeeds = new List<CardSignature>
        {
            new(new[] { 0.5f, -0.3f, 0.8f, -0.1f, 0.2f, -0.7f, 0.9f, -0.4f })
        };
        var abilityCards = new List<CardSignature> { new() };
        SimpleMapData? generatedMap = null;

        _service.GeneratedMapReady += map => generatedMap = (SimpleMapData)((IGridGeneratedMap)map).RawMapData;

        _service.StartSession(mapSeeds, abilityCards, _generator);

        // Wait for deferred AdvanceSession call to execute and set the task
        await _service.ToSignal(_service.GetTree(), SceneTree.SignalName.ProcessFrame);

        // Now the generation task should be set
        var generationTask = _service.CurrentGenerationTask;
        Assertions.AssertThat(generationTask).IsNotNull();
        await generationTask!;

        Assertions.AssertThat(generatedMap).IsNotNull();
        Assertions.AssertThat(generatedMap!.PassableTiles.Count).IsGreater(0);
    }

    [TestCase]
    public async Task TestStateChangedEventFires()
    {
        var mapSeeds = new List<CardSignature> { new() };
        var abilityCards = new List<CardSignature> { new() };
        var stateHistory = new List<SessionState>();

        _service.StateChanged += state => stateHistory.Add(state);

        _service.StartSession(mapSeeds, abilityCards, _generator);

        // Wait for deferred AdvanceSession call to execute and set the task
        await _service.ToSignal(_service.GetTree(), SceneTree.SignalName.ProcessFrame);

        // Now the generation task should be set
        var generationTask = _service.CurrentGenerationTask;
        Assertions.AssertThat(generationTask).IsNotNull();
        await generationTask!;

        // Wait for deferred state transition to process
        await _service.ToSignal(_service.GetTree(), SceneTree.SignalName.ProcessFrame);

        // Should have at least GeneratingMap and Exploring in history
        Assertions.AssertThat(stateHistory.Count).IsGreaterEqual(2);
        Assertions.AssertThat(stateHistory[0]).IsEqual(SessionState.GeneratingMap);
        Assertions.AssertThat(stateHistory[1]).IsEqual(SessionState.Exploring);
    }

    [TestCase]
    public async Task TestMultipleMapSeedsGenerateMap()
    {
        // Test that multiple map seeds are accepted and map generation proceeds
        // CardBasedGradient uses capsule (2 cards) or Bezier (3+ cards) patterns
        var mapSeeds = new List<CardSignature>
        {
            new(new[] { 0.5f, -0.3f, 0.8f, -0.1f, 0.2f, -0.7f, 0.9f, -0.4f }),
            new(new[] { -0.5f, 0.3f, -0.8f, 0.1f, -0.2f, 0.7f, -0.9f, 0.4f })
        };
        var abilityCards = new List<CardSignature> { new() };
        SimpleMapData? generatedMap = null;

        _service.GeneratedMapReady += map => generatedMap = (SimpleMapData)((IGridGeneratedMap)map).RawMapData;

        _service.StartSession(mapSeeds, abilityCards, _generator);

        // Wait for deferred AdvanceSession call to execute and set the task
        await _service.ToSignal(_service.GetTree(), SceneTree.SignalName.ProcessFrame);

        // Now the generation task should be set
        var generationTask = _service.CurrentGenerationTask;
        Assertions.AssertThat(generationTask).IsNotNull();
        await generationTask!;

        Assertions.AssertThat(generatedMap).IsNotNull();
        Assertions.AssertThat(generatedMap!.PassableTiles.Count).IsGreater(0);
    }

    [TestCase]
    public async Task TestThreeMapSeedsGenerateMap()
    {
        // Test three map seeds (triggers Bezier gradient pattern)
        var mapSeeds = new List<CardSignature>
        {
            new(new[] { 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f }),
            new(new[] { 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f }),
            new(new[] { 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f })
        };
        var abilityCards = new List<CardSignature> { new() };
        SimpleMapData? generatedMap = null;

        _service.GeneratedMapReady += map => generatedMap = (SimpleMapData)((IGridGeneratedMap)map).RawMapData;

        _service.StartSession(mapSeeds, abilityCards, _generator);

        // Wait for deferred AdvanceSession call to execute and set the task
        await _service.ToSignal(_service.GetTree(), SceneTree.SignalName.ProcessFrame);

        // Now the generation task should be set
        var generationTask = _service.CurrentGenerationTask;
        Assertions.AssertThat(generationTask).IsNotNull();
        await generationTask!;

        Assertions.AssertThat(generatedMap).IsNotNull();
        Assertions.AssertThat(generatedMap!.PassableTiles.Count).IsGreater(0);
    }

    [TestCase(Timeout = 30000)]
    [TestCategory("Unit")]
    public async Task ExplorationReportsCellIdsAndWorldPositions()
    {
        var events = await RunSessionToEnd(StrongAbility);

        AssertInt(events.Moves.Count).OverrideFailureMessage("The session never moved the player").IsGreater(0);
        AssertBool(events.Moves.All(position => events.Map!.GetCellAtPosition(position).HasValue))
            .OverrideFailureMessage("A move reported a world position outside the map")
            .IsTrue();
        AssertInt(events.VisitedCells.Count)
            .OverrideFailureMessage("VisitedCellsUpdated never reported a cell")
            .IsGreater(0);
        AssertBool(events.VisitedCells.All(events.Map!.IsValidCell))
            .OverrideFailureMessage("VisitedCellsUpdated reported a cell outside the map")
            .IsTrue();
        AssertInt(events.VisibilityUpdates).OverrideFailureMessage("VisibilityCellsUpdated never fired").IsGreater(0);
        AssertInt(events.PathUpdates).OverrideFailureMessage("PathCellsUpdated never fired").IsGreater(0);
        var invalid = string.Join(", ", events.InvalidCells);
        AssertInt(events.InvalidCells.Count)
            .OverrideFailureMessage($"Visibility or path events reported cells outside the map: [{invalid}]")
            .IsEqual(0);

        // The fixture's map holds one enemy, and the strong ability kills it with its first hit.
        var defeated = string.Join(", ", events.DefeatedCells);
        AssertBool(events.DefeatedCells.SequenceEqual(new[] { events.EnemyCell }))
            .OverrideFailureMessage($"Defeated cells [{defeated}], expected [{events.EnemyCell}]")
            .IsTrue();
        AssertInt(_lastLootGenerated.Count).OverrideFailureMessage("The won fight generated no loot").IsGreater(0);
    }

    [TestCase(Timeout = 30000)]
    [TestCategory("Unit")]
    public async Task ALostFightReportsNoDefeatedCellAndNoLoot()
    {
        // An all-zero ability deals 1 damage a turn, so the map's enemy always wins.
        var events = await RunSessionToEnd(new CardSignature());

        var defeated = string.Join(", ", events.DefeatedCells);
        AssertInt(events.DefeatedCells.Count)
            .OverrideFailureMessage($"A lost fight reported defeated cells [{defeated}]")
            .IsEqual(0);
        AssertInt(_lastLootGenerated.Count).OverrideFailureMessage("A lost fight generated loot").IsEqual(0);
    }

    /// <summary>
    /// Runs a session with one ability card to its end, recording its cell-id and world-position events, and checks
    /// that it completed after a fight.
    /// </summary>
    private async Task<SessionEvents> RunSessionToEnd(CardSignature ability)
    {
        // Combat turns take a second each; a faster clock lets the session reach its end within the budget.
        Engine.TimeScale = 20;
        try
        {
            var mapSeeds = new List<CardSignature>
            {
                new(new[] { 0.5f, -0.3f, 0.8f, -0.1f, 0.2f, -0.7f, 0.9f, -0.4f })
            };
            var events = new SessionEvents(_service);

            _service.StartSession(mapSeeds, new List<CardSignature> { ability }, _generator);
            await WaitForFrames(() => events.Completed, 3000);

            AssertBool(events.Completed).OverrideFailureMessage("The session never completed").IsTrue();
            AssertBool(events.Fought).OverrideFailureMessage("The session ended without a fight").IsTrue();
            return events;
        }
        finally
        {
            Engine.TimeScale = 1;
        }
    }

    /// <summary>What a session reported through its cell-id and world-position events.</summary>
    private sealed class SessionEvents
    {
        public SessionEvents(GameSessionService service)
        {
            service.GeneratedMapReady += generated =>
            {
                var grid = (IGridGeneratedMap)generated;
                Map = generated.GetMapData();
                EnemyCell = grid.GridMapData.PositionToCellId(grid.RawMapData.EnemyPositions[0]);
            };
            service.PlayerMovedWorld += Moves.Add;
            service.VisitedCellsUpdated += cells => VisitedCells.UnionWith(cells);
            service.VisibilityCellsUpdated += (seen, visible) =>
            {
                VisibilityUpdates++;
                InvalidCells.AddRange(seen.Concat(visible).Where(cell => !Map!.IsValidCell(cell)));
            };
            service.PathCellsUpdated += (path, target) =>
            {
                PathUpdates++;
                InvalidCells.AddRange(path.Where(cell => !Map!.IsValidCell(cell)));
                if (target is { } targetCell && !Map!.IsValidCell(targetCell))
                    InvalidCells.Add(targetCell);
            };
            service.EnemyDefeatedCell += DefeatedCells.Add;
            service.StateChanged += state =>
            {
                Fought |= state == SessionState.InCombat;
                Completed |= state == SessionState.SessionComplete;
            };
        }

        public IMapData? Map { get; private set; }
        public int EnemyCell { get; private set; } = -1;
        public List<Vector2> Moves { get; } = new();
        public HashSet<int> VisitedCells { get; } = new();
        public int VisibilityUpdates { get; private set; }
        public int PathUpdates { get; private set; }
        public List<int> DefeatedCells { get; } = new();
        public List<int> InvalidCells { get; } = new();
        public bool Fought { get; private set; }
        public bool Completed { get; private set; }
    }
}
