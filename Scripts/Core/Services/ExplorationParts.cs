using CardCleaner.Scripts.Core.Interfaces;

namespace CardCleaner.Scripts.Core.Services;

/// <summary>Builds the pathfinder and visibility checker an exploration uses; holds no state.</summary>
public static class ExplorationParts
{
    public static IPathfinder CreatePathfinder(IMapData mapData) => new Pathfinder(mapData);

    public static IVisibilityChecker CreateSimpleVisibilityChecker() => new SimpleVisibilityChecker();
}
