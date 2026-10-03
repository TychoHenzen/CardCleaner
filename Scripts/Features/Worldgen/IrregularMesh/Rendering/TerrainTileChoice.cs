using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.Rendering;

/// <summary>
/// Atlas tile chosen for one quad, with the terrain id it is counted under in build statistics.
/// </summary>
internal readonly record struct TerrainTileChoice(Vector2I AtlasCoords, string StatsKey);
