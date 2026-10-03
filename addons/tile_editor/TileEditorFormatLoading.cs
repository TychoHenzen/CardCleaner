#if TOOLS
using System.Collections.Generic;
using System.Linq;
using Godot;
using static CardCleaner.Addons.TileEditor.TileEditorJsonModels;

namespace CardCleaner.Addons.TileEditor;

public partial class TileEditorService
{
    private void LoadCustomAutoTileFormats(List<AutoTileFormatDataJson>? formatData)
    {
        if (formatData == null)
            return;

        foreach (var data in formatData)
        {
            if (string.IsNullOrEmpty(data.Name))
                continue;

            var bitmaskType = ParseBitmaskType(data.BitmaskType);
            var allowedBitmasks = data.AllowedBitmasks?.ToHashSet()
                ?? Features.Worldgen.AutoTiling.AutoTileFormatDefinition
                    .AllBitmasksFor(bitmaskType);
            var variantSizes = ParseVariantSizes(data.VariantSizes);
            var editableFormat = CreateEditableFormat(
                data.Name,
                bitmaskType,
                allowedBitmasks,
                variantSizes);
            _customAutoTileFormats.Add(editableFormat);
            RegisterCustomFormat(data.Name, bitmaskType, allowedBitmasks, variantSizes);
        }

        GD.Print(
            $"[TileEditorService] Loaded {_customAutoTileFormats.Count} "
            + "custom auto-tile formats");
    }

    private static Features.Worldgen.AutoTiling.BitmaskType ParseBitmaskType(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "edge4" => Features.Worldgen.AutoTiling.BitmaskType.Edge4,
            "full8" => Features.Worldgen.AutoTiling.BitmaskType.Full8,
            _ => Features.Worldgen.AutoTiling.BitmaskType.Corner4
        };
    }

    private static Dictionary<int, Vector2I> ParseVariantSizes(
        Dictionary<string, Vector2IData>? variantSizeData)
    {
        var variantSizes = new Dictionary<int, Vector2I>();
        if (variantSizeData == null)
            return variantSizes;

        foreach (var (bitmaskText, sizeData) in variantSizeData)
        {
            if (int.TryParse(bitmaskText, out var bitmask))
                variantSizes[bitmask] = new Vector2I(sizeData.X, sizeData.Y);
        }

        return variantSizes;
    }

    private static EditableAutoTileFormat CreateEditableFormat(
        string name,
        Features.Worldgen.AutoTiling.BitmaskType bitmaskType,
        IEnumerable<int> allowedBitmasks,
        Dictionary<int, Vector2I> variantSizes)
    {
        var format = new EditableAutoTileFormat
        {
            Name = name,
            BitmaskType = bitmaskType,
            AllowedBitmasks = allowedBitmasks.ToHashSet()
        };
        foreach (var bitmask in format.AllowedBitmasks)
        {
            var size = variantSizes.TryGetValue(bitmask, out var value)
                ? value
                : new Vector2I(1, 1);
            format.VariantMappings[bitmask] = new EditableFormatVariant
            {
                SizeX = size.X,
                SizeY = size.Y,
                OffsetX = 0,
                OffsetY = 0
            };
        }

        return format;
    }

    private static void RegisterCustomFormat(
        string name,
        Features.Worldgen.AutoTiling.BitmaskType bitmaskType,
        IEnumerable<int> allowedBitmasks,
        Dictionary<int, Vector2I> variantSizes)
    {
        var mappings = allowedBitmasks.ToDictionary(
            bitmask => bitmask,
            bitmask => new Features.Worldgen.AutoTiling.VariantDefinition(
                Vector2I.Zero,
                variantSizes.TryGetValue(bitmask, out var size)
                    ? size
                    : new Vector2I(1, 1),
                Vector2I.Zero));
        var definition = new Features.Worldgen.AutoTiling.AutoTileFormatDefinition(
            name,
            bitmaskType,
            allowedBitmasks.ToHashSet(),
            mappings,
            isBuiltIn: false);
        Features.Worldgen.AutoTiling.AutoTileFormatRegistry.Register(definition);
    }
}
#endif
