#if TOOLS
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CardCleaner.Addons.TileEditor;

internal sealed class TileAtlasTransitionValidator
{
    internal List<string> Validate(IEnumerable<EditableTile> sourceTiles)
    {
        var tiles = sourceTiles.ToList();
        var allTileIds = new HashSet<string>(tiles.Select(tile => tile.Id));
        var simpleTileIds = new HashSet<string>(
            tiles
                .Where(tile => !tile.HasAutoTileVariants)
                .Select(tile => tile.Id));
        var context = new ValidationContext(tiles, allTileIds, simpleTileIds);
        var warnings = new List<string>();

        foreach (var tile in tiles)
        {
            if (!tile.HasAutoTileVariants)
                continue;

            ValidateReference(
                tile,
                tile.InnerTerrainId,
                "InnerTerrainId",
                context,
                warnings);
            ValidateReference(
                tile,
                tile.OuterTerrainId,
                "OuterTerrainId",
                context,
                warnings,
                "*");

            if (tile.IsCompositable)
            {
                GD.Print(
                    $"[TileAtlasCompiler] Compositable auto-tile: {tile.Id} "
                    + "(will generate N×M combinations)");
            }
        }

        return warnings;
    }

    private static void ValidateReference(
        EditableTile tile,
        string? referenceId,
        string referenceName,
        ValidationContext context,
        List<string> warnings,
        string? ignoredReference = null)
    {
        if (string.IsNullOrEmpty(referenceId) || referenceId == ignoredReference)
            return;

        if (!context.AllTileIds.Contains(referenceId))
        {
            warnings.Add(
                $"Tile '{tile.Id}': {referenceName} '{referenceId}' "
                + "references non-existent tile");
            return;
        }

        if (context.SimpleTileIds.Contains(referenceId))
            return;

        var referencedTile = context.Tiles.FirstOrDefault(candidate => candidate.Id == referenceId);
        if (referencedTile?.HasAutoTileVariants == true)
        {
            warnings.Add(
                $"Tile '{tile.Id}': {referenceName} '{referenceId}' "
                + "references another auto-tile (expected simple tile)");
        }
    }

    private sealed class ValidationContext
    {
        internal ValidationContext(
            List<EditableTile> tiles,
            HashSet<string> allTileIds,
            HashSet<string> simpleTileIds)
        {
            Tiles = tiles;
            AllTileIds = allTileIds;
            SimpleTileIds = simpleTileIds;
        }

        internal List<EditableTile> Tiles { get; }
        internal HashSet<string> AllTileIds { get; }
        internal HashSet<string> SimpleTileIds { get; }
    }
}
#endif
