using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh.Debug;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.WorldMap;

/// <summary>
/// Snapshot of the world map screen settings and scene nodes used while building one generated map.
/// </summary>
internal sealed class WorldMapSettings
{
    internal required float WorldScale { get; init; }
    internal required float VisionRange { get; init; }
    internal required float MovementSpeed { get; init; }
    internal required bool FogOfWarEnabled { get; init; }
    internal required bool UseRaycastVisibility { get; init; }
    internal required SubViewport? Viewport { get; init; }
    internal required Camera2D? Camera { get; init; }
    internal required IrregularMeshDebugRenderer? DebugRenderer { get; init; }
    internal required ITileRegistry? TileRegistry { get; init; }
    internal required RandomNumberGenerator Rng { get; init; }
    internal required Node CombatHost { get; init; }
}
