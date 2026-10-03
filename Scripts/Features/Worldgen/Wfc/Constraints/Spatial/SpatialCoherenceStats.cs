using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;

/// <summary>
/// Diagnostic counters for one spatial coherence run. Printed and cleared when the next run starts.
/// </summary>
internal sealed class SpatialCoherenceStats
{
    // Number of matches whose details are logged to confirm coherence is active.
    private const int LoggedMatchCount = 5;

    private int _totalCalls;
    private int _callsWithMatch;
    private int _callsNoCollapsedNeighbor;
    private float _totalBoostApplied;

    internal void RecordCall()
    {
        _totalCalls++;
    }

    internal void RecordNoCollapsedNeighbor()
    {
        _callsNoCollapsedNeighbor++;
    }

    internal void RecordMatch(string tileId, int regionSize, bool isLinear)
    {
        _callsWithMatch++;

        if (_callsWithMatch <= LoggedMatchCount)
        {
            GD.Print(
                $"[SpatialCoherence] Match #{_callsWithMatch}: tile={tileId}, " +
                $"regionSize={regionSize}, isLinear={isLinear}");
        }
    }

    internal void RecordBoost(float modifier)
    {
        _totalBoostApplied += modifier;
    }

    /// <summary>
    /// Prints the previous run stats, if any, then zeroes every counter.
    /// </summary>
    internal void PrintAndClear()
    {
        if (_totalCalls > 0)
        {
            var matchRate = _callsWithMatch * 100f / _totalCalls;
            var avgBoost = _callsWithMatch > 0 ? _totalBoostApplied / _callsWithMatch : 0;
            GD.Print(
                $"[SpatialCoherence] Stats: {_totalCalls} calls, {_callsWithMatch} with match ({matchRate:F1}%), " +
                $"{_callsNoCollapsedNeighbor} no collapsed neighbor, avg boost {avgBoost:F2}x");
        }

        _totalCalls = 0;
        _callsWithMatch = 0;
        _callsNoCollapsedNeighbor = 0;
        _totalBoostApplied = 0;
    }
}
