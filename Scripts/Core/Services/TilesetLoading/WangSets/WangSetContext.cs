using CardCleaner.Features.Worldgen.AutoTiling;

namespace CardCleaner.Scripts.Core.Services.TilesetLoading.WangSets;

/// <summary>
/// The per-set settings shared by every wangtile of one Tiled wangset element.
/// </summary>
internal sealed record WangSetContext(string SetId, string WangType, BitmaskType BitmaskType, int TerrainColorIndex);
