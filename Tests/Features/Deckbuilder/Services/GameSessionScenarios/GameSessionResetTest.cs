using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using Godot;


namespace CardCleaner.Tests.Features.Deckbuilder.Services.GameSessionScenarios;

/// <summary>
///     GameSessionService reset and deferred generation cancellation scenarios split out of GameSessionServiceTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class GameSessionResetTest : GameSessionServiceTestBase
{
    [TestCase]
    public async Task ResetSession_CancelsDeferredGenerationBeforeItStarts()
    {
        var generator = new CancellationBlockingMapGenerator();
        _service.SetMapGenerator(generator);
        _service.StartSession(
            new List<CardSignature> { new() },
            new List<CardSignature> { new() });

        var pendingGenerationTask = _service.CurrentGenerationTask;
        Assertions.AssertThat(pendingGenerationTask).IsNotNull();

        _service.ResetSession();
        await _service.ToSignal(_service.GetTree(), SceneTree.SignalName.ProcessFrame);

        Assertions.AssertBool(pendingGenerationTask!.IsCanceled).IsTrue();
        Assertions.AssertBool(generator.Started.Task.IsCompleted).IsFalse();
        Assertions.AssertThat(_service.CurrentState).IsEqual(SessionState.WaitingForCards);
        Assertions.AssertThat(_service.CurrentGenerationTask).IsNull();
    }

    [TestCase]
    public async Task ResetSession_StaleGenerationCannotOverrideNextSession()
    {
        var generator = new CancellationBlockingMapGenerator();
        var generatedMapEventCount = 0;
        _service.GeneratedMapReady += _ => generatedMapEventCount++;
        _service.SetMapGenerator(generator);
        _service.StartSession(
            new List<CardSignature> { new() },
            new List<CardSignature> { new() });

        await _service.ToSignal(_service.GetTree(), SceneTree.SignalName.ProcessFrame);
        await generator.Started.Task;
        var staleGenerationTask = _service.CurrentGenerationTask;
        Assertions.AssertThat(staleGenerationTask).IsNotNull();

        _service.ResetSession();
        await staleGenerationTask!;
        await _service.ToSignal(_service.GetTree(), SceneTree.SignalName.ProcessFrame);
        await _service.ToSignal(_service.GetTree(), SceneTree.SignalName.ProcessFrame);

        Assertions.AssertThat(_service.CurrentState).IsEqual(SessionState.WaitingForCards);
        Assertions.AssertThat(CountProgressNodes()).IsEqual(0);

        _service.SetMapGenerator(CreateFastMapGenerator());
        _service.StartSession(
            new List<CardSignature> { new() },
            new List<CardSignature> { new() });
        await _service.ToSignal(_service.GetTree(), SceneTree.SignalName.ProcessFrame);
        var currentGenerationTask = _service.CurrentGenerationTask;
        Assertions.AssertThat(currentGenerationTask).IsNotNull();
        await currentGenerationTask!;
        await _service.ToSignal(_service.GetTree(), SceneTree.SignalName.ProcessFrame);
        await _service.ToSignal(_service.GetTree(), SceneTree.SignalName.ProcessFrame);

        Assertions.AssertThat(_service.CurrentState).IsEqual(SessionState.Exploring);
        Assertions.AssertThat(_service.CurrentGeneratedMap).IsNotNull();
        Assertions.AssertThat(generatedMapEventCount).IsEqual(1);
        Assertions.AssertThat(CountProgressNodes()).IsEqual(0);
    }

    private sealed class CancellationBlockingMapGenerator : IMapGenerator
    {
        public TaskCompletionSource<bool> Started { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<IGeneratedMap> GenerateAsync(
            MapGenerationConfig config,
            IProgress<float>? progress = null,
            CancellationToken cancellationToken = default)
        {
            Started.TrySetResult(true);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return null!;
        }
    }
}
