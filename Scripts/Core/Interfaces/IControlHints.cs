using System;
using System.Collections.Generic;

namespace CardCleaner.Scripts.Core.Interfaces;

/// <summary>
///     The key hints the player's HUD shows. A component that gives keys a meaning while it is in some state (a
///     container in hand, a shelf being placed) publishes its hints under itself when that state starts and clears
///     them when it ends, so the HUD never needs to know the component.
/// </summary>
public interface IControlHints
{
    /// <summary>Raised after any source's hints change.</summary>
    event Action? Changed;

    /// <summary>Every source's hints, in the order the sources first published.</summary>
    IReadOnlyList<ControlHint> Hints { get; }

    /// <summary>Replaces the hints of <paramref name="source" />. An empty list clears them.</summary>
    void Publish(object source, IReadOnlyList<ControlHint> hints);

    /// <summary>Removes the hints of <paramref name="source" />, if it has any.</summary>
    void Clear(object source);
}
