using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using Godot;


namespace CardCleaner.Tests.Features.Deckbuilder.Services.GameSessionScenarios;

/// <summary>
///     Shared fixture for the GameSessionService scenario suites.
/// </summary>
public abstract class GameSessionServiceTestBase
{
    protected GameSessionService _service = null!;
    protected SessionState _lastStateChange;
    protected List<CardSignature> _lastLootGenerated = new();
    protected int _stateChangeCount;
    protected TileRegistry _tileRegistry = null!;

    [BeforeTest]
    public async Task Setup()
    {
        // Load the real tile registry so map generation works
        _tileRegistry = new TileRegistry();
        _tileRegistry.LoadFromData();

        // Register with ServiceLocator so GameSessionService can resolve it
        ServiceLocator.Container.RegisterSingleton<ITileRegistry>(_tileRegistry);
        ServiceLocator.Container.RegisterSingleton<ITileMetadataProvider>(_tileRegistry);

        _service = new GameSessionService();
        Assertions.AddNode(_service);
        await ISceneRunner.SyncProcessFrame;

        _lastStateChange = SessionState.WaitingForCards;
        _lastLootGenerated = new List<CardSignature>();
        _stateChangeCount = 0;

        _service.StateChanged += OnStateChanged;
        _service.LootGenerated += OnLootGenerated;
    }

    protected void OnStateChanged(SessionState newState)
    {
        _lastStateChange = newState;
        _stateChangeCount++;
    }

    protected void OnLootGenerated(List<CardSignature> loot)
    {
        _lastLootGenerated = loot;
    }

    protected int CountProgressNodes()
    {
        var count = 0;
        foreach (var child in _service.GetChildren())
        {
            if (child is GodotProgress)
                count++;
        }

        return count;
    }

    /// <summary>
    ///     Advances process frames until the condition holds or the frame budget is spent.
    /// </summary>
    protected async Task WaitForFrames(Func<bool> done, int maxFrames)
    {
        var frameCount = 0;
        while (!done() && frameCount < maxFrames)
        {
            await _service.ToSignal(_service.GetTree(), SceneTree.SignalName.ProcessFrame);
            frameCount++;
        }
    }
}
