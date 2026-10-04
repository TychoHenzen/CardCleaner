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
            Id,
            Name,
            Common.Passability,
            AtlasCoords,
            new TileDefinitionOptions
            {
                SourceId = SourceId,
                Layer = Common.Layer,
                Elevation = Common.Elevation,
                IsTransparent = Common.IsTransparent,
                AllowedBiomes = Biomes,
                Size = Size,
                DecorationDensity = Common.DecorationDensity,
                AutoTileVariants = AutoTileVariants,
                AutoTileFormatName = AutoTileFormat,
                Variations = Variations,
                VariationMode = VariationMode,
                Animation = Animation,
                Dominance = Common.Dominance,
                InnerTerrainId = Common.InnerTerrain,
                OuterTerrainId = Common.OuterTerrain,
                IsGapTile = IsGapTile,
                Probability = Probability
            });
    }
}
