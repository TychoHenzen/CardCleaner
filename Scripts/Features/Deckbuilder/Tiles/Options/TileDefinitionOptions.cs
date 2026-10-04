using System.Collections.Generic;
using CardCleaner.Scripts.Core.Enumeration;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Tiles;

/// <summary>
/// Optional settings for a <see cref="TileDefinition"/>; every property carries the default a tile gets
/// when the setting is not specified.
/// </summary>
internal sealed class TileDefinitionOptions
{
    internal int SourceId { get; init; } = 4;
    internal TileLayer Layer { get; init; } = TileLayer.Terrain;
    internal float Elevation { get; init; }

    /// <summary>When null, a tile is transparent exactly when it is passable.</summary>
    internal bool? IsTransparent { get; init; }
    internal HashSet<string>? AllowedBiomes { get; init; }
    internal Vector2I? Size { get; init; }
    internal float DecorationDensity { get; init; } = 1.0f;
    internal Vector2I?[]? AutoTileVariants { get; init; }
    internal string AutoTileFormatName { get; init; } = "corner16";
    internal Vector2I[]? Variations { get; init; }
    internal VariationMode VariationMode { get; init; } = VariationMode.PerInstance;
    internal TileAnimation? Animation { get; init; }
    internal int Dominance { get; init; }
    internal string? InnerTerrainId { get; init; }
    internal string? OuterTerrainId { get; init; }
    internal bool IsGapTile { get; init; }
    internal float Probability { get; init; } = 1.0f;
}
