using System.Text.RegularExpressions;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;

namespace CardCleaner.Scripts.Core.Services.TilesetLoading;

/// <summary>
/// Detects variation patterns from tile IDs.
/// </summary>
internal static class VariationPatternDetector
{
    /// <summary>
    /// Regex patterns for detecting variation groups from tile IDs.
    /// Numbered suffix (grass1, grass2) = PerGeneration (per-map), lettered suffix (flower_a, flower_b) = PerInstance.
    /// </summary>
    private static readonly Regex NumberedSuffixPattern =
        new(@"^(.+?)(\d+)$", RegexOptions.Compiled);

    private static readonly Regex LetteredSuffixPattern =
        new(@"^(.+)_([a-z])$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Detects variation group info from a tile ID based on naming patterns.
    /// Numbered suffixes (grass1, grass2, grass3) = PerGeneration (one picked per map).
    /// Lettered suffixes (flower_a, flower_b) = PerInstance (randomly picked per placement).
    /// </summary>
    /// <param name="tileId">The tile ID to analyze.</param>
    /// <returns>Variation info or null if no pattern detected.</returns>
    internal static VariationGroupInfo? DetectVariationPattern(string tileId)
    {
        if (string.IsNullOrEmpty(tileId))
            return null;

        // Check for numbered suffix first (grass1, grass2) - per-map variation
        var numberedMatch = NumberedSuffixPattern.Match(tileId);
        if (numberedMatch.Success)
        {
            var baseName = numberedMatch.Groups[1].Value;
            var variantIndex = int.Parse(numberedMatch.Groups[2].Value);

            // Avoid matching IDs that are just numbers or have very short base names
            if (!string.IsNullOrEmpty(baseName) && baseName.Length >= 2)
            {
                return new VariationGroupInfo(baseName, variantIndex, VariationMode.PerGeneration);
            }
        }

        // Check for lettered suffix (flower_a, flower_b) - per-instance variation
        var letteredMatch = LetteredSuffixPattern.Match(tileId);
        if (letteredMatch.Success)
        {
            var baseName = letteredMatch.Groups[1].Value;
            var letter = letteredMatch.Groups[2].Value.ToLowerInvariant()[0];
            var variantIndex = letter - 'a'; // a=0, b=1, c=2, etc.

            if (!string.IsNullOrEmpty(baseName))
            {
                return new VariationGroupInfo(baseName, variantIndex, VariationMode.PerInstance);
            }
        }

        return null;
    }
}
