using System.Collections.Generic;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using Godot;

namespace CardCleaner.Tests.Features.Deckbuilder.Services.GameSessionScenarios;

/// <summary>
/// The view paces exploration: after a move the session waits for the view's report before its next step.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class GameSessionPacingTest : GameSessionServiceTestBase
{
    // Ten times the session's 0.1 s exploration delay: without the hold, several more steps would run in it.
    private const double HoldSeconds = 1.0;
    private const int FrameBudget = 300;

    protected override bool ReportsStepsShown => false;

    [TestCase(Timeout = 20000)]
    [TestCategory("Unit")]
    public async Task ExplorationWaitsForTheViewToReportEachMoveShown()
    {
        var moves = 0;
        _service.PlayerMovedWorld += _ => moves++;
        _service.StartSession(
            new List<CardSignature> { new(new[] { 0.5f, -0.3f, 0.8f, -0.1f, 0.2f, -0.7f, 0.9f, -0.4f }) },
            new List<CardSignature> { new() },
            _generator);

        await WaitForFrames(() => moves > 0, FrameBudget);
        AssertThat(moves).OverrideFailureMessage("The session never moved the player").IsEqual(1);

        await _service.ToSignal(_service.GetTree().CreateTimer(HoldSeconds), SceneTreeTimer.SignalName.Timeout);
        AssertThat(moves).OverrideFailureMessage("The session stepped on without the view's report").IsEqual(1);
        AssertThat(_service.CurrentState).IsEqual(SessionState.Exploring);

        _service.ReportStepShown();
        await WaitForFrames(() => moves > 1 || _service.CurrentState != SessionState.Exploring, FrameBudget);
        AssertBool(moves > 1 || _service.CurrentState != SessionState.Exploring)
            .OverrideFailureMessage("The session did not resume after the view's report")
            .IsTrue();
    }
}
