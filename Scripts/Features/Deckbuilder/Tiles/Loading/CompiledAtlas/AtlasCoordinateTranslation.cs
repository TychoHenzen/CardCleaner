using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Tiles.Loading.CompiledAtlas;

/// <summary>
/// Where a tile coordinate lands. <see cref="Mapped"/> is false when the mapping had no entry for it and the
/// original coordinates came back unchanged.
/// </summary>
internal readonly record struct AtlasCoordinateTranslation(
    int SourceId,
    Vector2I AtlasCoords)
{
    internal bool Mapped { get; init; }
}
