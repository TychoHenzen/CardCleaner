#if TOOLS
using System.Collections.Generic;
using System.Linq;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// Matrix tracking transition coverage between terrain pairs.
/// </summary>
internal class TransitionCoverageMatrix
{
    public List<string> TerrainIds { get; set; } = new();
    private readonly Dictionary<(string inner, string outer), TransitionStatus> _coverage = new();

    public int TotalTransitions => TerrainIds.Count > 0
        ? TerrainIds.Count * (TerrainIds.Count - 1)
        : 0;

    public int CoveredCount => _coverage.Count(kvp =>
        kvp.Value is TransitionStatus.Compositable or TransitionStatus.Fixed);

    public int MissingCount => TotalTransitions - CoveredCount;

    public void SetCoverage(string innerTerrain, string outerTerrain, TransitionStatus status)
    {
        _coverage[(innerTerrain, outerTerrain)] = status;
    }

    public TransitionStatus GetStatus(string innerTerrain, string outerTerrain)
    {
        if (innerTerrain == outerTerrain)
            return TransitionStatus.Self;

        return _coverage.TryGetValue((innerTerrain, outerTerrain), out var status)
            ? status
            : TransitionStatus.Missing;
    }
}
#endif
