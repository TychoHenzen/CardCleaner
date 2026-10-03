using CardCleaner.Scripts.Core.Enumeration;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Components.WorldMapTerrainRendererSupport;

public readonly record struct TileRenderInfo(
    int SourceId,
    Vector2I AtlasCoords,
    TileLayer Layer,
    Vector2I Size);
