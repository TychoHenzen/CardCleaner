using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features.Deckbuilder.Services;

/// <summary>
/// Diagnostic test to identify passability mismatches between biomes and TileRegistry.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TilePassabilityDiagnosticTest
{
    [TestCase]
    public void FindPassabilityMismatches()
    {
        var registry = new BiomeRegistry();
        registry.RegisterDefaultBiomes();

        var tileRegistry = new TileRegistry();

        var missingTiles = new List<string>();
        var passabilityMismatches = new List<string>();

        foreach (var biome in registry.GetAllBiomes())
        {
            GD.Print($"\n=== Biome: {biome.Id} ===");

            foreach (var tileId in biome.PassableTiles.GetAllTileIds())
            {
                var tile = tileRegistry.GetTile(tileId);

                if (tile == null)
                {
                    missingTiles.Add($"{biome.Id}/{tileId}");
                    GD.Print($"  MISSING: {tileId} - not in TileRegistry!");
                }
                else if (!tile.IsPassable)
                {
                    passabilityMismatches.Add($"{biome.Id}/{tileId}");
                    GD.Print($"  MISMATCH: {tileId} - in PassableTiles but IsPassable=false");
                }
            }

            foreach (var tileId in biome.BlockedTiles.GetAllTileIds())
            {
                var tile = tileRegistry.GetTile(tileId);

                if (tile == null)
                {
                    GD.Print($"  BLOCKED MISSING: {tileId} - not in TileRegistry");
                }
                else if (tile.IsPassable)
                {
                    GD.Print($"  BLOCKED MISMATCH: {tileId} - in BlockedTiles but IsPassable=true");
                }
            }
        }

        GD.Print($"\n=== SUMMARY ===");
        GD.Print($"Missing tiles: {missingTiles.Count}");
        foreach (var t in missingTiles) GD.Print($"  - {t}");

        GD.Print($"Passability mismatches: {passabilityMismatches.Count}");
        foreach (var t in passabilityMismatches) GD.Print($"  - {t}");

        // This test is diagnostic - it should pass but print useful info
        // If we find mismatches, that's our root cause
        if (missingTiles.Count > 0 || passabilityMismatches.Count > 0)
        {
            GD.Print("\n*** FOUND ISSUES - These cause disconnected maps! ***");
        }
    }
}
