using CardCleaner.Scripts.Core.Services.CompiledAtlas;
using System.Linq;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.Wfc.Generation;

/// <summary>
/// Pins the rule tile ids a terrain solver reports: the transition-pair ids in insertion order, taken before
/// WfcMapGenerator adds the gap tiles to the same rules.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class WfcTerrainSolverRuleTileIdsTest
{
    private const string ProbeGapTileId = "rule-snapshot-probe-gap";

    [TestCase]
    [TestCategory("Unit")]
    public static void RuleTileIdsAreTheTransitionIdsBeforeGapTilesAreAdded()
    {
        var transitionPairs = new CompiledTransitionResolver(new GodotAssetPaths()).GetAllTransitionPairs().ToList();
        var registry = TileCatalogLoad.Load(new GodotAssetPaths(), new CompiledAtlasSource());
        // A passable tile without auto-tile variants is a gap tile. Its id is unique, so no transition names it.
        registry.RegisterTile(new TileDefinition(ProbeGapTileId, "Probe", TilePassability.Passable, Vector2I.Zero));

        var expected = new WfcAdjacencyRules(transitionPairs);
        // Precondition: gap configuration adds the probe to fresh rules, so a snapshot taken after it would contain it.
        var withGaps = new WfcAdjacencyRules(transitionPairs);
        GapTileAdjacencyConfigurator.Configure(withGaps, TileCatalogData.From(registry));
        AssertBool(withGaps.AllTileIds.Contains(ProbeGapTileId)).IsTrue();
        AssertBool(expected.AllTileIds.Contains(ProbeGapTileId)).IsFalse();

        var solver = new WfcTerrainSolver(transitionPairs, TileCatalogData.From(registry));

        AssertString(string.Join("|", solver.RuleTileIds)).IsEqual(string.Join("|", expected.AllTileIds));
    }
}
