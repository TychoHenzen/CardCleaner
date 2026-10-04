using Godot;

namespace CardCleaner.Scripts.Core.Services.CompiledAtlas;

internal readonly record struct AtlasCoordinateTranslation(
    int SourceId,
    Vector2I AtlasCoords);
