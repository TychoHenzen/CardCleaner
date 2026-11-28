using System.Collections.Generic;
using System.Linq;

namespace CardCleaner.Scripts.Features.Shop.Restocking;

/// <summary>
///     The restocker's job state: idle, walking to the source, walking to the shelf. A pair whose target was lost
///     stays blocked for <see cref="BlockSeconds" /> so the planner picks another. A walk with no path also blocks the
///     source against the target's whole shelf for as long, since every slot of that shelf stands in the same place:
///     blocking one slot at a time sent the restocker on one hopeless walk per slot.
/// </summary>
public sealed class RestockerJob
{
    public const double BlockSeconds = 20;

    private readonly Dictionary<(int Source, int Target), double> _blockedUntil = new();
    private readonly Dictionary<(int Source, int Shelf), double> _unreachableUntil = new();
    private double _now;

    public RestockerPhase Phase { get; private set; } = RestockerPhase.Idle;

    public RestockStep Current { get; private set; } = RestockStep.Idle;

    public IReadOnlySet<(int Source, int Target)> BlockedPairs => _blockedUntil.Keys.ToHashSet();

    /// <summary>Sources paired with a shelf a walk could not reach, while the block lasts.</summary>
    public IReadOnlySet<(int Source, int Shelf)> UnreachableShelves => _unreachableUntil.Keys.ToHashSet();

    public void Begin(RestockStep step)
    {
        if (Phase != RestockerPhase.Idle || step.Kind == RestockStepKind.Idle)
        {
            return;
        }

        Current = step;
        Phase = RestockerPhase.ToSource;
    }

    /// <summary>Marks the current leg done. True when the stock was placed (the second arrival).</summary>
    public bool Arrived()
    {
        switch (Phase)
        {
            case RestockerPhase.ToSource:
                Phase = RestockerPhase.ToShelf;
                return false;
            case RestockerPhase.ToShelf:
                Reset();
                return true;
            default:
                return false;
        }
    }

    public void TargetLost() => Abandon(shelfUnreachable: false);

    public void NoPath() => Abandon(shelfUnreachable: true);

    /// <summary>Drops the walk without blocking its pair: the restocker was dismissed, nothing went wrong.</summary>
    public void Cancel() => Reset();

    public void Tick(double seconds)
    {
        _now += seconds;
        foreach (var pair in _blockedUntil.Where(p => p.Value <= _now).Select(p => p.Key).ToList())
        {
            _blockedUntil.Remove(pair);
        }

        foreach (var pair in _unreachableUntil.Where(p => p.Value <= _now).Select(p => p.Key).ToList())
        {
            _unreachableUntil.Remove(pair);
        }
    }

    private void Abandon(bool shelfUnreachable)
    {
        if (Phase == RestockerPhase.Idle)
        {
            return;
        }

        _blockedUntil[(Current.SourceId, Current.TargetId)] = _now + BlockSeconds;
        if (shelfUnreachable && Current.ShelfId >= 0)
        {
            _unreachableUntil[(Current.SourceId, Current.ShelfId)] = _now + BlockSeconds;
        }

        Reset();
    }

    private void Reset()
    {
        Current = RestockStep.Idle;
        Phase = RestockerPhase.Idle;
    }
}
