#if TOOLS
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CardCleaner.Addons.TileEditor;

public partial class TileEditorService
{
    private static readonly Regex TileIdPattern = new(
        @"^[a-z][a-z0-9_]*$",
        RegexOptions.Compiled);

    public TileEditorOperationResult ValidateTile(EditableTile tile)
    {
        if (string.IsNullOrWhiteSpace(tile.Id))
            return new TileEditorOperationResult(false, "ID is required");

        if (!TileIdPattern.IsMatch(tile.Id))
            return new TileEditorOperationResult(
                false,
                "ID must be snake_case starting with a letter");

        if (string.IsNullOrWhiteSpace(tile.Name))
            return new TileEditorOperationResult(false, "Name is required");

        var validPassability = new[] { "passable", "solid", "partially_passable" };
        if (!validPassability.Contains(tile.Passability.ToLowerInvariant()))
            return new TileEditorOperationResult(false, "Invalid passability value");

        var validLayers = new[] { "terrain", "decoration", "structure", "effects" };
        if (!validLayers.Contains(tile.Layer.ToLowerInvariant()))
            return new TileEditorOperationResult(false, "Invalid layer value");

        if (_biomes.Count == 0)
            return new TileEditorOperationResult(true, "Valid");

        foreach (var biome in tile.Biomes)
        {
            if (!_biomes.ContainsKey(biome.ToLowerInvariant()))
                return new TileEditorOperationResult(false, $"Invalid biome: {biome}");
        }

        return new TileEditorOperationResult(true, "Valid");
    }
}
#endif
