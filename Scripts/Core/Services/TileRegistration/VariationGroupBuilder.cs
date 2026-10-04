using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services.TilesetLoading;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;

namespace CardCleaner.Scripts.Core.Services;

/// <summary>
/// Builds variation groups from loaded tiles based on naming patterns.
/// Tiles with numbered suffixes (grass1, grass2) are grouped as PerGeneration.
/// Tiles with lettered suffixes (flower_a, flower_b) are grouped as PerInstance.
/// </summary>
internal static class VariationGroupBuilder
{
    /// <summary>
    /// Replaces the contents of <paramref name="target"/> with the groups detected in <paramref name="tiles"/>.
    /// </summary>
    internal static void Rebuild(IEnumerable<TileDefinition> tiles, VariationGroupCollection target)
    {
        target.Clear();

        foreach (var (baseName, members) in GroupByPattern(tiles))
        {
            // Groups need 2+ tiles
            if (members.Count < 2)
                continue;

            // Use the mode from the first member (they should all be the same)
            var mode = members[0].Info.Mode;

            // Build variants with weights from tile probability
            var variants = members
                .OrderBy(m => m.Info.VariantIndex) // Sort by variant index for consistency
                .Select(m => new VariantWeight(m.Tile.Id, m.Tile.Probability))
                .ToList();

            target.AddGroup(new VariationGroup(baseName, mode, variants));
        }

        if (target.Count > 0)
        {
            ILog.Print($"[TileRegistry] Built {target.Count} variation groups from tile naming patterns");
        }
    }

    private static Dictionary<string, List<GroupMember>> GroupByPattern(IEnumerable<TileDefinition> tiles)
    {
        var groups = new Dictionary<string, List<GroupMember>>();

        foreach (var tile in tiles)
        {
            var info = TiledTilesetLoader.DetectVariationPattern(tile.Id);
            if (info == null)
                continue;

            if (!groups.ContainsKey(info.BaseName))
                groups[info.BaseName] = new List<GroupMember>();

            groups[info.BaseName].Add(new GroupMember(tile, info));
        }

        return groups;
    }

    private readonly record struct GroupMember(TileDefinition Tile, VariationGroupInfo Info);
}
