namespace CardCleaner.Scripts.Core.Interfaces;

/// <summary>
/// Creates the per-session world and exploration session. Stateless: each call returns a new, independent instance.
/// </summary>
public interface ISessionFactory
{
    ISessionWorld CreateWorld();

    IExplorationSession CreateExploration(ISessionWorld world);
}
