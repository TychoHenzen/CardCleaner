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

        _service.GeneratedMapReady += map => generatedMap = ((IGridGeneratedMap)map).RawMapData;

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

        _service.GeneratedMapReady += map => generatedMap = ((IGridGeneratedMap)map).RawMapData;

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

        _service.GeneratedMapReady += map => generatedMap = ((IGridGeneratedMap)map).RawMapData;

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
        // Combat turns take a second each; a faster clock lets the session reach its end within the budget.
        Engine.TimeScale = 20;
        try
        {
            var mapSeeds = new List<CardSignature>
            {
                new(new[] { 0.5f, -0.3f, 0.8f, -0.1f, 0.2f, -0.7f, 0.9f, -0.4f })
            };
            IMapData? map = null;
            var enemyCell = -1;
            var moves = new List<Vector2>();
            var visitedCells = new HashSet<int>();
            var visibilityUpdates = 0;
            var pathUpdates = 0;
            var defeatedCells = new List<int>();
            var invalidCells = new List<int>();
            var completed = false;

            _service.GeneratedMapReady += generated =>
            {
                var grid = (IGridGeneratedMap)generated;
                map = generated.GetMapData();
                enemyCell = grid.GridMapData.PositionToCellId(grid.RawMapData.EnemyPositions.First());
            };
            _service.PlayerMovedWorld += moves.Add;
            _service.VisitedCellsUpdated += cells => visitedCells.UnionWith(cells);
            _service.VisibilityCellsUpdated += (seen, visible) =>
            {
                visibilityUpdates++;
                invalidCells.AddRange(seen.Concat(visible).Where(cell => !map!.IsValidCell(cell)));
            };
            _service.PathCellsUpdated += (path, target) =>
            {
                pathUpdates++;
                invalidCells.AddRange(path.Where(cell => !map!.IsValidCell(cell)));
                if (target is { } targetCell && !map!.IsValidCell(targetCell))
                    invalidCells.Add(targetCell);
            };
            _service.EnemyDefeatedCell += defeatedCells.Add;
            _service.StateChanged += state => completed |= state == SessionState.SessionComplete;

            _service.StartSession(mapSeeds, new List<CardSignature> { new() }, _generator);
            await WaitForFrames(() => completed, 3000);

            AssertBool(completed).OverrideFailureMessage("The session never completed").IsTrue();
            AssertBool(moves.Count > 0).OverrideFailureMessage("The session never moved the player").IsTrue();
            AssertBool(moves.All(position => map!.GetCellAtPosition(position).HasValue))
                .OverrideFailureMessage("A move reported a world position outside the map")
                .IsTrue();
            AssertBool(visitedCells.Count > 0 && visitedCells.All(map!.IsValidCell)).IsTrue();
            AssertInt(visibilityUpdates).IsGreater(0);
            AssertInt(pathUpdates).IsGreater(0);
            AssertInt(invalidCells.Count).IsEqual(0);

            // The fixture's map holds one enemy: a won fight reports its cell, a lost fight reports none.
            var expectedDefeated = _lastLootGenerated.Count > 0 ? new List<int> { enemyCell } : new List<int>();
            AssertBool(defeatedCells.SequenceEqual(expectedDefeated))
                .OverrideFailureMessage(
                    $"Defeated cells [{string.Join(", ", defeatedCells)}], expected [{string.Join(", ", expectedDefeated)}]")
                .IsTrue();
        }
        finally
        {
            Engine.TimeScale = 1;
        }
    }
}
