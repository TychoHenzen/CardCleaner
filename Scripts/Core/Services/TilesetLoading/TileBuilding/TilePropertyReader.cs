using System.Collections.Generic;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using Godot;

namespace CardCleaner.Scripts.Core.Services.TilesetLoading.TileBuilding;

/// <summary>
/// Reads typed tile values out of the raw string property bag of a tile or Wang set.
/// </summary>
internal static class TilePropertyReader
{
    internal static TileCommonProperties ReadCommon(Dictionary<string, string> props)
    {
        var passability = ParseHelpers.ParsePassability(ParseHelpers.GetString(props, "passability", "passable"));
        return new TileCommonProperties
        {
            Passability = passability,
            Layer = ParseHelpers.ParseLayer(ParseHelpers.GetString(props, "layer", "terrain")),
            Elevation = ParseHelpers.GetFloat(props, "elevation", 0f),
            IsTransparent = ParseHelpers.GetBool(props, "istransparent", passability == TilePassability.Passable),
            Dominance = ParseHelpers.GetInt(props, "dominance", 0),
            DecorationDensity = ParseHelpers.GetFloat(props, "decorationdensity", 1f),
            OuterTerrain = ParseHelpers.GetStringOrNull(props, "outerterrain"),
            InnerTerrain = ParseHelpers.GetStringOrNull(props, "innerterrain")
        };
    }

    internal static Vector2I? ReadSize(Dictionary<string, string> props)
    {
        var sizeStr = ParseHelpers.GetStringOrNull(props, "size");
        if (string.IsNullOrEmpty(sizeStr))
            return null;

        var sizeParts = sizeStr.Split(',', 'x');
        return sizeParts.Length == 2
            ? new Vector2I(ParseHelpers.ParseInt(sizeParts[0], 1), ParseHelpers.ParseInt(sizeParts[1], 1))
            : null;
    }
}
