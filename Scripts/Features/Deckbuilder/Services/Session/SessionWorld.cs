using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Randomness;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services.Session;

/// <summary>
/// The map and random number generator produced by the current session's generation.
/// </summary>
internal sealed class SessionWorld : ISessionWorld
{
    public IRandom Rng { get; set; } = RandomSources.Create();

    /// <summary>Unified map interface.</summary>
    public IGeneratedMap? GeneratedMap { get; set; }

    /// <summary>Pathfinding adapter.</summary>
    public IMapData? MapData { get; set; }

    public int EnemyCount => GeneratedMap?.EnemyCount ?? 0;

    public void ClearMap()
    {
        GeneratedMap = null;
        MapData = null;
    }
}
