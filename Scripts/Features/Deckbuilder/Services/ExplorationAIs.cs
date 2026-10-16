using CardCleaner.Scripts.Core.Interfaces;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services;

public static class ExplorationAIs
{
    public static IExplorationAI Create(
        IMapData mapData,
        int? startCell = null,
        IVisibilityChecker? visibilityChecker = null) =>
        new ExplorationAI(mapData, startCell, visibilityChecker);
}
