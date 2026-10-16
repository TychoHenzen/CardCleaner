using CardCleaner.Scripts.Core.Interfaces;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services.Session;

/// <summary>The map and random number generator produced by the current session's generation.</summary>
internal interface ISessionWorld
{
    IRandom Rng { get; set; }
    IGeneratedMap? GeneratedMap { get; set; }
    IMapData? MapData { get; set; }
    int EnemyCount { get; }
    void ClearMap();
}
