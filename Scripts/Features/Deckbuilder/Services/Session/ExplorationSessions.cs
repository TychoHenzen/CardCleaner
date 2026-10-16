namespace CardCleaner.Scripts.Features.Deckbuilder.Services.Session;

internal static class ExplorationSessions
{
    internal static IExplorationSession Create(ISessionWorld world) => new ExplorationSession(world);
}
