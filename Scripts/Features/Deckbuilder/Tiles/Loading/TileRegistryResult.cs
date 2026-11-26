using System.Collections.Generic;
using CardCleaner.Features.Deckbuilder.Tiles;

namespace CardCleaner.Scripts.Features.Deckbuilder.Tiles.Loading;

/// <summary>
/// Result of loading tile registry data
/// </summary>
public record TileRegistryResult(string TilesetPath, List<TileDefinition> Tiles, TilesetConfig TilesetConfig);
