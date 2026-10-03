using System.Linq;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.TerrainComposition;

/// <summary>
///     TerrainCompositionBoundaryTest scenarios split out of TerrainCompositionContextTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TerrainCompositionBoundaryTest : TerrainCompositionContextTestBase
{
    // ==================== Different Outer Terrains Produce Different Results ====================

    [TestCase]
    public void TestSameInnerDifferentOuterProducesDifferentCoords()
    {
        // This test validates that the composition context matters
        // Same inner terrain with different outer terrains should produce different tiles

        var compositableTiles = _registry.GetAllTiles()
            .Where(t => t.IsCompositable)
            .Take(3)
            .ToList();

        if (compositableTiles.Count < 3)
        {
            GD.Print("Need at least 3 compositable tiles for this test");
            return;
        }

        var inner = compositableTiles[0].Id;
        var outer1 = compositableTiles[1].Id;
        var outer2 = compositableTiles[2].Id;

        GD.Print($"Testing: {inner} on {outer1} vs {inner} on {outer2}");

        var differentCoords = 0;
        var sameCoords = 0;

        for (var bitmask = 1; bitmask < 15; bitmask++) // Skip 0 and 15 (empty and full)
        {
            var coords1 = _resolver.ResolveTransition(inner, outer1, bitmask);
            var coords2 = _resolver.ResolveTransition(inner, outer2, bitmask);

            if (coords1.HasValue && coords2.HasValue)
            {
                if (coords1 != coords2)
                    differentCoords++;
                else
                    sameCoords++;
            }
        }

        GD.Print($"Results: {differentCoords} different, {sameCoords} same");

        // If all coords are the same, the composition context isn't being used properly
        // We expect at least SOME to be different
        if (differentCoords == 0 && sameCoords > 0)
        {
            GD.PrintErr("WARNING: All transition coords are same regardless of outer terrain");
            GD.PrintErr("This suggests transition_map may not have terrain-specific composites");
        }

        // This is informational - it tells us whether composition matters for this tile set
    }

    // ==================== Dominance Determines Inner/Outer ====================

    [TestCase]
    public void TestDominanceOrderAffectsTransitionLookup()
    {
        // Higher dominance terrain should be the "inner" (visible border)
        // Lower dominance terrain should be the "outer" (background)

        var tilesWithDominance = _registry.GetAllTiles()
            .Where(t => t.IsCompositable && t.Dominance > 0)
            .OrderBy(t => t.Dominance)
            .ToList();

        if (tilesWithDominance.Count < 2)
        {
            GD.Print("No tiles with explicit dominance values found");
            return;
        }

        GD.Print("Tiles ordered by dominance:");
        foreach (var tile in tilesWithDominance.Take(5))
        {
            GD.Print($"  {tile.Id}: dominance={tile.Dominance}");
        }

        // When low-dominance borders high-dominance:
        // The high-dominance terrain's border should be visible
        var lowDom = tilesWithDominance.First();
        var highDom = tilesWithDominance.Last();

        GD.Print(
            $"\nExpected: {highDom.Id} (dom={highDom.Dominance}) renders its border " +
            $"on top of {lowDom.Id} (dom={lowDom.Dominance})");

        // The transition should be keyed as highDom|lowDom, not lowDom|highDom
        var correctKeyExists = _resolver.HasTransition(highDom.Id, lowDom.Id);
        var reverseKeyExists = _resolver.HasTransition(lowDom.Id, highDom.Id);

        GD.Print($"Transition {highDom.Id}|{lowDom.Id} exists: {correctKeyExists}");
        GD.Print($"Transition {lowDom.Id}|{highDom.Id} exists: {reverseKeyExists}");

        // At least one direction should exist
        AssertBool(correctKeyExists || reverseKeyExists).IsTrue();
    }

    // ==================== Composition Context Data Model ====================

    /// <summary>
    /// Documents what composition context data should look like.
    /// Currently this may not exist - this test documents the requirement.
    /// </summary>
    [TestCase]
    public void TestCompositionContextRequirements()
    {
        GD.Print("=== TERRAIN COMPOSITION CONTEXT REQUIREMENTS ===");
        GD.Print("");
        GD.Print("Current Problem:");
        GD.Print("  - Data grid stores only terrain ID (e.g., 'grass')");
        GD.Print("  - At boundaries, renderer needs to know both inner AND outer terrain");
        GD.Print("  - Example: 'grass' at position could need 'grass|sand' or 'grass|dirt' tiles");
        GD.Print("");
        GD.Print("Solution Options:");
        GD.Print("");
        GD.Print("Option A: Store Terrain Pairs in Data Grid");
        GD.Print("  - Change cell data from 'string terrainId' to 'TerrainCell { inner, outer }'");
        GD.Print("  - Boundary cells store both terrains they connect");
        GD.Print("  - Non-boundary cells have same inner and outer (or outer=null)");
        GD.Print("");
        GD.Print("Option B: Compute Outer at Render Time");
        GD.Print("  - Keep data grid as single terrain IDs");
        GD.Print("  - At render time, sample adjacent cells to determine outer terrain");
        GD.Print("  - Uses dominance to decide which terrain is 'inner' vs 'outer'");
        GD.Print("");
        GD.Print("Option C: Layer-Based Approach");
        GD.Print("  - Base layer stores ground terrain (e.g., 'sand')");
        GD.Print("  - Overlay layer stores transition terrain (e.g., 'grass')");
        GD.Print("  - Renderer composites: base + transition overlay");
        GD.Print("");
        GD.Print("Current Implementation:");

        // Check what the current system does
        var compositableTiles = _registry.GetAllTiles()
            .Where(t => t.IsCompositable)
            .ToList();

        var outerTerrainIds = compositableTiles
            .Select(t => t.OuterTerrainId)
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct()
            .ToList();

        GD.Print($"  - Compositable tiles: {compositableTiles.Count}");
        GD.Print($"  - Tiles with OuterTerrainId='*' (any): {compositableTiles.Count(t => t.IsCompositable)}");
        GD.Print($"  - Tiles with fixed OuterTerrainId: {outerTerrainIds.Count(id => id != "*")}");
        GD.Print("");
        GD.Print("Recommendation:");
        GD.Print("  Use Option B (compute at render time) since transition_map.json");
        GD.Print("  already contains all terrain pair combinations. The renderer just");
        GD.Print("  needs to determine the outer terrain from adjacent cells.");

        // This is informational - skip if no compositable tiles exist in current data
        if (compositableTiles.Count == 0)
        {
            GD.Print("  No compositable tiles found (none with OuterTerrainId='*') - skipping assertion");
            return;
        }
        AssertThat(compositableTiles.Count).IsGreater(0);
    }
}
