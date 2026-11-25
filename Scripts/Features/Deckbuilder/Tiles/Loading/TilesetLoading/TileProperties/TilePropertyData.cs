using System.Collections.Generic;

namespace CardCleaner.Scripts.Features.Deckbuilder.Tiles.Loading.TilesetLoading.TileProperties;

/// <summary>
/// Container for parsed tile properties.
/// </summary>
internal sealed record TilePropertyData(
    string? Type,
    Dictionary<string, string> Properties,
    List<AnimationFrame>? Animation);
