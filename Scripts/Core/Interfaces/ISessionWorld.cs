namespace CardCleaner.Scripts.Core.Interfaces;

/// <summary>
/// The map and random number generator produced by the current session's generation.
/// </summary>
public interface ISessionWorld
{
    IRandom Rng { get; set; }

    IGeneratedMap? GeneratedMap { get; set; }

    IMapData? MapData { get; set; }

    int EnemyCount { get; }

    void ClearMap();
}
