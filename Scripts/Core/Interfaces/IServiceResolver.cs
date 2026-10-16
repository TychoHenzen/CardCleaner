using System;
using System.Diagnostics.CodeAnalysis;

namespace CardCleaner.Scripts.Core.Interfaces;

/// <summary>
///     Looks services up for a node that the composition root injected (see <see cref="IDependent" />). The
///     composition root's ServiceHost implements it and reads its current container at call time, so a resolver stays
///     valid when the container is rebuilt.
/// </summary>
[SuppressMessage("Naming", "CA1716:Identifiers should not match keywords",
    Justification = "Get is the lookup verb of the container API that every injected node calls; " +
                    "C# is the only consumer.")]
public interface IServiceResolver
{
    /// <summary>Returns the registered service; throws InvalidOperationException when it is not registered.</summary>
    T Get<T>() where T : class;

    bool Has<T>() where T : class;

    /// <summary>
    ///     Runs the callback now when the service is registered; otherwise queues it, and it runs once when the
    ///     composition root next runs the pending callbacks with the service registered.
    /// </summary>
    void Get<T>(Action<T> callback) where T : class;
}
