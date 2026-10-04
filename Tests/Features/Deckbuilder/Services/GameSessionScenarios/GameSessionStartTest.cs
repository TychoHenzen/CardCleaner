using System.Collections.Generic;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services;


namespace CardCleaner.Tests.Features.Deckbuilder.Services.GameSessionScenarios;

/// <summary>
///     GameSessionService initial state and session start scenarios split out of GameSessionServiceTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class GameSessionStartTest : GameSessionServiceTestBase
{
    [TestCase]
    public void TestInitialState()
    {
        Assertions.AssertThat(_service.CurrentState).IsEqual(SessionState.WaitingForCards);
    }

    [TestCase]
    public void TestStartSessionWithValidInputs()
    {
        var mapSeeds = new List<CardSignature>
        {
            new(new[] { 0.5f, -0.3f, 0.8f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f })
        };
        var abilityCards = new List<CardSignature>
        {
            new(new[] { 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f }),
            new(new[] { 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f })
        };

        _service.StartSession(mapSeeds, abilityCards);

        Assertions.AssertThat(_service.CurrentState).IsEqual(SessionState.GeneratingMap);
        Assertions.AssertThat(_stateChangeCount).IsEqual(1);
        Assertions.AssertThat(_lastStateChange).IsEqual(SessionState.GeneratingMap);
    }

    [TestCase]
    public void TestStartSessionWithNullMapSeeds()
    {
        var abilityCards = new List<CardSignature>
        {
            new(new[] { 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f })
        };

        _service.StartSession(null, abilityCards);

        Assertions.AssertThat(_service.CurrentState).IsEqual(SessionState.WaitingForCards);
        Assertions.AssertThat(_stateChangeCount).IsEqual(0);
    }

    [TestCase]
    public void TestStartSessionWithEmptyMapSeeds()
    {
        var emptyMapSeeds = new List<CardSignature>();
        var abilityCards = new List<CardSignature>
        {
            new(new[] { 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f })
        };

        _service.StartSession(emptyMapSeeds, abilityCards);

        Assertions.AssertThat(_service.CurrentState).IsEqual(SessionState.WaitingForCards);
        Assertions.AssertThat(_stateChangeCount).IsEqual(0);
    }

    [TestCase]
    public void TestStartSessionWithEmptyAbilityCards()
    {
        var mapSeeds = new List<CardSignature>
        {
            new(new[] { 0.5f, -0.3f, 0.8f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f })
        };
        var emptyAbilityCards = new List<CardSignature>();

        _service.StartSession(mapSeeds, emptyAbilityCards);

        Assertions.AssertThat(_service.CurrentState).IsEqual(SessionState.WaitingForCards);
        Assertions.AssertThat(_stateChangeCount).IsEqual(0);
    }

    [TestCase]
    public void TestStartSessionWhenNotWaiting()
    {
        var mapSeeds = new List<CardSignature> { new() };
        var abilityCards = new List<CardSignature> { new() };
        _service.StartSession(mapSeeds, abilityCards);

        _stateChangeCount = 0;

        _service.StartSession(mapSeeds, abilityCards);

        Assertions.AssertThat(_stateChangeCount).IsEqual(0);
    }

    [TestCase]
    public void TestResetSession()
    {
        var mapSeeds = new List<CardSignature> { new() };
        var abilityCards = new List<CardSignature> { new() };
        _service.StartSession(mapSeeds, abilityCards);

        _service.ResetSession();

        Assertions.AssertThat(_service.CurrentState).IsEqual(SessionState.WaitingForCards);
    }
}
