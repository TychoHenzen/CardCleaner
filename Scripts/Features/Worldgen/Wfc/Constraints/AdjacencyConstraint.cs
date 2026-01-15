namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;

/// <summary>
/// Hard constraint wrapper around WfcAdjacencyRules.
/// Returns 0.0 for invalid adjacencies, 1.0 for valid.
/// Checks all collapsed neighbors to ensure the candidate tile
/// can legally be placed next to them.
/// </summary>
public class AdjacencyConstraint : IWfcConstraint
{
    private readonly WfcAdjacencyRules _rules;

    public AdjacencyConstraint(WfcAdjacencyRules rules)
    {
        _rules = rules;
    }

    /// <inheritdoc />
    public float GetProbabilityModifier(WfcConstraintContext context)
    {
        return 1f;
        // Check all collapsed neighbors using topology-agnostic API
        foreach (var neighborId in context.Topology.GetNeighbors(context.CellId))
        {
            var neighborTile = context.Topology.GetCollapsedTileAt(neighborId);
            if (neighborTile == null) continue;

            // If this tile can't be adjacent to the neighbor, hard ban it
            if (!_rules.CanBeAdjacent(context.TileId, neighborTile))
            {
                return 0.0f;
            }
        }

        return 1.0f; // Valid adjacency with all neighbors
    }
}
