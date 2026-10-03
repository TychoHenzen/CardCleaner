using System.Collections.Generic;
using CardCleaner.Features.Worldgen.AutoTiling;

namespace CardCleaner.Scripts.Core.Services.TilesetLoading.WangSets;

/// <summary>
/// Metadata about a Wang set.
/// </summary>
internal sealed record WangSetInfo(string Name, BitmaskType BitmaskType, Dictionary<string, string> Properties);
