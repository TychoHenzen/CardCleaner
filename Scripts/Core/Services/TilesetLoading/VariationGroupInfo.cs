using CardCleaner.Scripts.Features.Deckbuilder.Tiles;

namespace CardCleaner.Scripts.Core.Services.TilesetLoading;

/// <summary>Describes a tile's membership in a variation group.</summary>
public record VariationGroupInfo(string BaseName, int VariantIndex, VariationMode Mode);
