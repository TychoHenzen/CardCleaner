using System.Collections.Generic;
using System.Linq;

namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>
///     The restocker's job state: idle, walking to the source, walking to the shelf. A pair whose target was lost
///     or unreachable stays blocked for <see cref="BlockSeconds" /> so the planner picks another.
/// </summary>
public sealed class RestockerJob
{
    public const double BlockSeconds = 20;

    private readonly Dictionary<(int Source, int Target), double> _blockedUntil = new();
    private double _now;

    public RestockerPhase Phase { get; private set; } = RestockerPhase.Idle;

    public RestockStep Current { get; private set; } = RestockStep.Idle;

    public IReadOnlySet<(int Source, int Target)> BlockedPairs => _blockedUntil.Keys.ToHashSet();

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

    public void TargetLost() => Abandon();

    public void NoPath() => Abandon();

    public void Tick(double seconds)
    {
        _now += seconds;
        foreach (var pair in _blockedUntil.Where(p => p.Value <= _now).Select(p => p.Key).ToList())
        {
            _blockedUntil.Remove(pair);
        }
    }

    private void Abandon()
    {
        if (Phase == RestockerPhase.Idle)
        {
            return;
        }

        _blockedUntil[(Current.SourceId, Current.TargetId)] = _now + BlockSeconds;
        Reset();
    }

    private void Reset()
    {
        Current = RestockStep.Idle;
        Phase = RestockerPhase.Idle;
    }
}
