using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.Wfc.Generation;

/// <summary>
/// Pins the rule tile ids a terrain solver reports: the adjacency ids in insertion order, taken before
/// WfcMapGenerator adds the gap tiles to the same rules.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class WfcTerrainSolverRuleTileIdsTest
{
    private const string ProbeGapTileId = "rule-snapshot-probe-gap";

    [TestCase]
    [TestCategory("Unit")]
    public static void RuleTileIdsAreTheAdjacencyIdsBeforeGapTilesAreAdded()
    {
        var adjacency = new Dictionary<string, HashSet<string>>
        {
            { "alpha", new HashSet<string> { "beta", "gamma" } },
            { "gamma", new HashSet<string> { "delta" } }
        };
        var registry = new TileRegistry();
        // A passable tile without auto-tile variants is a gap tile. Its id is unique, so no transition names it.
        registry.RegisterTile(new TileDefinition(ProbeGapTileId, "Probe", TilePassability.Passable, Vector2I.Zero));

        var expected = AdjacencyRules(adjacency);
        // Precondition: gap configuration adds the probe to fresh rules, so a snapshot taken after it would contain it.
        var withGaps = AdjacencyRules(adjacency);
        GapTileAdjacencyConfigurator.Configure(withGaps, registry);
        AssertBool(withGaps.AllTileIds.Contains(ProbeGapTileId)).IsTrue();
        AssertBool(expected.AllTileIds.Contains(ProbeGapTileId)).IsFalse();

        var transitionPairs = new CompiledTransitionResolver().GetAllTransitionPairs().ToList();
        var solver = IWfcTerrainSolver.Create(transitionPairs, adjacency, registry);

        AssertString(string.Join("|", solver.RuleTileIds)).IsEqual(string.Join("|", expected.AllTileIds));
    }

    private static WfcAdjacencyRules AdjacencyRules(Dictionary<string, HashSet<string>> adjacency)
    {
        var rules = new WfcAdjacencyRules(new CompiledTransitionResolver().GetAllTransitionPairs().ToList());
        foreach (var (tile, neighbors) in adjacency)
        {
            foreach (var neighbor in neighbors)
            {
                rules.AddAdjacency(tile, neighbor);
            }
        }

        return rules;
    }
}
