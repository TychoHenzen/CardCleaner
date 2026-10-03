#if TOOLS
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// Mutable tile data for editing
/// </summary>
public class EditableTile
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Passability { get; set; } = "passable";
    public int AtlasX { get; set; }
    public int AtlasY { get; set; }
    public int SourceId { get; set; } = 4;
    public string Layer { get; set; } = "terrain";
    public float Elevation { get; set; }
    public bool IsTransparent { get; set; } = true;
    public List<string> Biomes { get; set; } = new();
    public int SizeX { get; set; } = 1;
    public int SizeY { get; set; } = 1;

    /// <summary>Source texture scale: 0.5 for 32px, 1.0 for 16px, 2.0 for 8px tiles.</summary>
    public float SourceScale { get; set; } = 1.0f;

    /// <summary>Auto-tile atlas coordinates by bitmask; null elements use base coordinates.</summary>
    public Vector2I?[]? AutoTileVariants { get; set; }

    /// <summary>Auto-tile format name; built-ins and registered custom formats are supported.</summary>
    public string AutoTileFormat { get; set; } = "corner16";

    /// <summary>Per-bitmask variant size/offset definitions; null or empty uses 1x1 variants.</summary>
    public Dictionary<int, EditableVariantDefinition>? CustomVariantDefinitions { get; set; }

    /// <summary>
    /// Returns true if this tile has any auto-tile variants defined.
    /// </summary>
    public bool HasAutoTileVariants => AutoTileVariants?.Any(v => v.HasValue) == true;

    /// <summary>
    /// Get the expected number of variants based on the format.
    /// </summary>
    public int ExpectedVariantCount => AutoTileFormat == "blob47" ? 47 : 16;

    /// <summary>
    /// Probability (0.0-1.0) of this decoration tile appearing on valid positions.
    /// Only meaningful for decoration layer tiles. Default 1.0 = 100% coverage.
    /// </summary>
    public float DecorationDensity { get; set; } = 1.0f;

    /// <summary>
    /// Visual variations for this tile (different atlas coordinates for the same tile type).
    /// Null means no variations (always use base AtlasCoords).
    /// </summary>
    public Vector2I[]? Variations { get; set; }

    /// <summary>Variation mode: perinstance randomizes placements; pergeneration shares one choice.</summary>
    public string VariationMode { get; set; } = "perinstance";

    /// <summary>
    /// Whether this tile has visual variations.
    /// </summary>
    public bool HasVariations => Variations != null && Variations.Length > 0;

    /// <summary>
    /// Animation frame atlas coordinates. Null means no animation.
    /// </summary>
    public Vector2I[]? AnimationFrames { get; set; }

    /// <summary>
    /// Duration of each animation frame in seconds.
    /// </summary>
    public float AnimationFrameDuration { get; set; } = 0.2f;

    /// <summary>
    /// Whether this tile has animation frames.
    /// </summary>
    public bool HasAnimation => AnimationFrames != null && AnimationFrames.Length > 1;

    /// <summary>
    /// The tile mode determining which features are active.
    /// Stored as lowercase string for JSON serialization.
    /// </summary>
    public string TileMode { get; set; } = "plain";

    /// <summary>Terrain transition dominance; higher values render above lower values.</summary>
    public int? Dominance { get; set; }

    /// <summary>
    /// For auto-tiles: the terrain whose border is shown (higher dominance terrain).
    /// Null means use dominance-based resolution at runtime.
    /// </summary>
    public string? InnerTerrainId { get; set; }

    /// <summary>Background terrain for auto-tiles; "*" is compositable, null uses dominance resolution.</summary>
    public string? OuterTerrainId { get; set; }

    /// <summary>
    /// Visual appearance description for artists and AI image generators.
    /// Should include color, texture, and notable visual features.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Returns true if this auto-tile is compositable (OuterTerrainId is "*"),
    /// meaning its border should be composited onto any base terrain at atlas compile time.
    /// </summary>
    public bool IsCompositable => OuterTerrainId == "*";

    /// <summary>
    /// Returns true if this auto-tile has a fixed transition (OuterTerrainId is a specific tile ID),
    /// meaning it has a baked background and should be used as-is.
    /// </summary>
    public bool IsFixedTransition => !string.IsNullOrEmpty(OuterTerrainId) && OuterTerrainId != "*";

    /// <summary>
    /// Returns true if this tile has custom variant definitions with non-standard sizes or offsets.
    /// </summary>
    public bool HasCustomVariantDefinitions => CustomVariantDefinitions?.Count > 0;

    /// <summary>
    /// Gets the variant definition for a specific bitmask index.
    /// Returns a default definition with the atlas coords if no custom definition exists.
    /// </summary>
    public EditableVariantDefinition GetVariantDefinition(int bitmaskIndex)
    {
        if (CustomVariantDefinitions != null &&
            CustomVariantDefinitions.TryGetValue(bitmaskIndex, out var customDef))
        {
            return customDef;
        }

        if (AutoTileVariants != null && bitmaskIndex < AutoTileVariants.Length &&
            AutoTileVariants[bitmaskIndex].HasValue)
        {
            var coords = AutoTileVariants[bitmaskIndex]!.Value;
            return new EditableVariantDefinition
            {
                AtlasX = coords.X,
                AtlasY = coords.Y,
                SizeX = 1,
                SizeY = 1,
                OffsetX = 0,
                OffsetY = 0
            };
        }

        return new EditableVariantDefinition
        {
            AtlasX = AtlasX,
            AtlasY = AtlasY,
            SizeX = 1,
            SizeY = 1,
            OffsetX = 0,
            OffsetY = 0
        };
    }

    /// <summary>
    /// Sets a custom variant definition for a specific bitmask index.
    /// </summary>
    public void SetVariantDefinition(int bitmaskIndex, EditableVariantDefinition definition)
    {
        CustomVariantDefinitions ??= new Dictionary<int, EditableVariantDefinition>();
        CustomVariantDefinitions[bitmaskIndex] = definition;

        AutoTileVariants ??= new Vector2I?[ExpectedVariantCount];
        if (bitmaskIndex < AutoTileVariants.Length)
            AutoTileVariants[bitmaskIndex] = new Vector2I(definition.AtlasX, definition.AtlasY);
    }

    public EditableTile Clone()
    {
        var clone = new EditableTile
        {
            Id = Id,
            Name = Name,
            Passability = Passability,
            AtlasX = AtlasX,
            AtlasY = AtlasY,
            SourceId = SourceId,
            Layer = Layer,
            Elevation = Elevation,
            IsTransparent = IsTransparent,
            Biomes = new List<string>(Biomes),
            SizeX = SizeX,
            SizeY = SizeY,
            SourceScale = SourceScale,
            DecorationDensity = DecorationDensity,
            AutoTileFormat = AutoTileFormat,
            VariationMode = VariationMode,
            AnimationFrameDuration = AnimationFrameDuration,
            TileMode = TileMode,
            Dominance = Dominance,
            InnerTerrainId = InnerTerrainId,
            OuterTerrainId = OuterTerrainId,
            Description = Description
        };

        if (AutoTileVariants != null)
        {
            clone.AutoTileVariants = new Vector2I?[AutoTileVariants.Length];
            Array.Copy(AutoTileVariants, clone.AutoTileVariants, AutoTileVariants.Length);
        }

        if (Variations != null)
        {
            clone.Variations = new Vector2I[Variations.Length];
            Array.Copy(Variations, clone.Variations, Variations.Length);
        }

        if (AnimationFrames != null)
        {
            clone.AnimationFrames = new Vector2I[AnimationFrames.Length];
            Array.Copy(AnimationFrames, clone.AnimationFrames, AnimationFrames.Length);
        }

        if (CustomVariantDefinitions != null)
        {
            clone.CustomVariantDefinitions = new Dictionary<int, EditableVariantDefinition>();
            foreach (var (key, value) in CustomVariantDefinitions)
                clone.CustomVariantDefinitions[key] = value.Clone();
        }

        return clone;
    }
}
#endif
