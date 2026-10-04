using System.Collections.Generic;
using CardCleaner.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;

namespace CardCleaner.Scripts.Core.Services;

/// <summary>
/// Result of loading tile registry data
/// </summary>
public record TileRegistryResult(string TilesetPath, List<TileDefinition> Tiles, TilesetConfig TilesetConfig);
