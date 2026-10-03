#if TOOLS
using System.Collections.Generic;
using System.Linq;

namespace CardCleaner.Addons.TileEditor;

public partial class TransitionCoveragePanel
{
    private TransitionCoverageMatrix CalculateCoverageMatrix()
    {
        var matrix = new TransitionCoverageMatrix();
        var allTiles = _service!.AllTiles.ToList();
        var baseTerrains = allTiles
            .Where(tile => tile.Layer == "terrain" && !tile.HasAutoTileVariants)
            .Select(tile => tile.Id)
            .ToHashSet();
        var autoTiles = allTiles.Where(tile => tile.HasAutoTileVariants).ToList();
        var innerTerrainIds = GetInnerTerrainIds(autoTiles);
        var allTerrainIds = baseTerrains.Union(innerTerrainIds).OrderBy(id => id).ToList();
        matrix.TerrainIds = allTerrainIds;
        ApplyCoverage(matrix, autoTiles, allTerrainIds);
        return matrix;
    }

    private static HashSet<string> GetInnerTerrainIds(IEnumerable<EditableTile> autoTiles)
    {
        var innerTerrainIds = new HashSet<string>();
        foreach (var autoTile in autoTiles)
        {
            var innerTerrain = autoTile.InnerTerrainId ?? autoTile.Id;
            innerTerrainIds.Add(innerTerrain);
        }

        return innerTerrainIds;
    }

    private static void ApplyCoverage(
        TransitionCoverageMatrix matrix,
        IEnumerable<EditableTile> autoTiles,
        IReadOnlyCollection<string> allTerrainIds)
    {
        foreach (var autoTile in autoTiles)
        {
            var innerTerrain = autoTile.InnerTerrainId ?? autoTile.Id;
            if (autoTile.IsCompositable)
            {
                ApplyCompositableCoverage(matrix, innerTerrain, allTerrainIds);
                continue;
            }

            if (autoTile.IsFixedTransition)
                ApplyFixedCoverage(matrix, autoTile, innerTerrain, allTerrainIds);
        }
    }

    private static void ApplyCompositableCoverage(
        TransitionCoverageMatrix matrix,
        string innerTerrain,
        IEnumerable<string> allTerrainIds)
    {
        foreach (var outerTerrain in allTerrainIds)
        {
            if (innerTerrain == outerTerrain)
                continue;

            matrix.SetCoverage(innerTerrain, outerTerrain, TransitionStatus.Compositable);
            matrix.SetCoverage(outerTerrain, innerTerrain, TransitionStatus.Compositable);
        }
    }

    private static void ApplyFixedCoverage(
        TransitionCoverageMatrix matrix,
        EditableTile autoTile,
        string innerTerrain,
        IReadOnlyCollection<string> allTerrainIds)
    {
        var outerTerrain = autoTile.OuterTerrainId!;
        if (!allTerrainIds.Contains(outerTerrain) || innerTerrain == outerTerrain)
            return;

        matrix.SetCoverage(innerTerrain, outerTerrain, TransitionStatus.Fixed);
        matrix.SetCoverage(outerTerrain, innerTerrain, TransitionStatus.Fixed);
    }
}
#endif
