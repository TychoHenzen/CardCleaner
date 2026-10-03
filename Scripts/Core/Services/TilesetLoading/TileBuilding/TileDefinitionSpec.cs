using System.Collections.Generic;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using Godot;

namespace CardCleaner.Scripts.Core.Services.TilesetLoading.TileBuilding;

/// <summary>
/// Fully resolved field set for one tile, turned into a TileDefinition in a single place.
/// </summary>
internal sealed class TileDefinitionSpec
{
    internal required string Id { get; init; }
    internal required string Name { get; init; }
    internal required Vector2I AtlasCoords { get; init; }
    internal required int SourceId { get; init; }
    internal required TileCommonProperties Common { get; init; }
    internal required HashSet<string>? Biomes { get; init; }
    internal Vector2I? Size { get; init; }
    internal Vector2I?[]? AutoTileVariants { get; init; }
    internal required string AutoTileFormat { get; init; }
    internal Vector2I[]? Variations { get; init; }
    internal VariationMode VariationMode { get; init; } = VariationMode.PerInstance;
    internal TileAnimation? Animation { get; init; }
    internal required bool IsGapTile { get; init; }
    internal required float Probability { get; init; }

    internal TileDefinition ToDefinition()
    {
        return new TileDefinition(
            id: Id,
            name: Name,
            passability: Common.Passability,
            atlasCoords: AtlasCoords,
            sourceId: SourceId,
            layer: Common.Layer,
            elevation: Common.Elevation,
            isTransparent: Common.IsTransparent,
            allowedBiomes: Biomes,
            size: Size,
            decorationDensity: Common.DecorationDensity,
            autoTileVariants: AutoTileVariants,
            autoTileFormatName: AutoTileFormat,
            variations: Variations,
            variationMode: VariationMode,
            animation: Animation,
            dominance: Common.Dominance,
            innerTerrainId: Common.InnerTerrain,
            outerTerrainId: Common.OuterTerrain,
            isGapTile: IsGapTile,
            probability: Probability);
    }
}
