using CardCleaner.Scripts.Core.Interfaces;

namespace CardCleaner.Scripts.Core.Services;

public static class Pathfinders
{
    public static IPathfinder Create(IMapData mapData) => new Pathfinder(mapData);
}
