namespace CardCleaner.Scripts.Features.Deckbuilder.Services.Session;

internal static class SessionFactories
{
    internal static ISessionWorld CreateWorld() => new SessionWorld();

    internal static IExplorationSession CreateExploration(ISessionWorld world) => new ExplorationSession(world);
}
