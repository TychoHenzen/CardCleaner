using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services.Session;

/// <summary>
/// The map and random number generator produced by the current session's generation.
/// </summary>
internal sealed class SessionWorld
{
    internal RandomNumberGenerator Rng { get; set; } = new();

    /// <summary>Unified map interface.</summary>
    internal IGeneratedMap? GeneratedMap { get; set; }

    /// <summary>Pathfinding adapter.</summary>
    internal IMapData? MapData { get; set; }

    internal int EnemyCount => GeneratedMap?.EnemyCount ?? 0;

    internal void ClearMap()
    {
        GeneratedMap = null;
        MapData = null;
    }
}
