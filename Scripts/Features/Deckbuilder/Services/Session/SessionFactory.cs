using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services.Session;

/// <summary>
/// Builds the per-session world and exploration session; the container finds it by its [Service] attribute.
/// </summary>
[Service(typeof(ISessionFactory))]
internal sealed class SessionFactory : ISessionFactory
{
    public ISessionWorld CreateWorld() => new SessionWorld();

    public IExplorationSession CreateExploration(ISessionWorld world) => new ExplorationSession(world);
}
