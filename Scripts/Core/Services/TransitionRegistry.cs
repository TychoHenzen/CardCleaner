using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Worldgen.Transitions;
using Godot;

namespace CardCleaner.Scripts.Core.Services;

/// <summary>
/// Central service for terrain transitions.
/// Combines terrain groups, transition rules, and calculation logic.
/// </summary>
public class TransitionRegistry : ITransitionRegistry
{
    private readonly TerrainGroupRegistry _groupRegistry;
    private readonly TransitionRuleRegistry _ruleRegistry;
    private readonly TransitionCalculator _calculator;

    public BlobGenerationConfig BlobConfig { get; }
    public TerrainGroupRegistry GroupRegistry => _groupRegistry;

    public TransitionRegistry()
    {
        _groupRegistry = new TerrainGroupRegistry();
        _ruleRegistry = new TransitionRuleRegistry();

        // Load transition data from tiles.json
        var transitionData = TileDataLoader.LoadTransitionData();

        foreach (var group in transitionData.Groups)
            _groupRegistry.RegisterGroup(group);

        foreach (var rule in transitionData.Rules)
            _ruleRegistry.RegisterRule(rule);

        BlobConfig = transitionData.BlobConfig;

        _calculator = new TransitionCalculator(_groupRegistry, _ruleRegistry);

        ILog.Print($"[TransitionRegistry] Initialized with {_groupRegistry.Count} terrain groups, " +
                   $"{_ruleRegistry.Count} transition rules, blob generation {(BlobConfig.Enabled ? "enabled" : "disabled")}");
    }

    public TerrainGroup? GetGroupForTile(string tileId) =>
        _groupRegistry.GetGroupForTile(tileId);

    public string? GetTransitionTileId(Vector2I position, string[,] tileIds, Vector2I mapSize) =>
        _calculator.GetTransitionTileId(position, tileIds, mapSize);

    public int CalculateBitmask(Vector2I position, string[,] tileIds, Vector2I mapSize) =>
        _calculator.CalculateBitmask(position, tileIds, mapSize);
}
