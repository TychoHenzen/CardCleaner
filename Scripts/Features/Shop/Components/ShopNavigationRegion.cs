using System;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     Bakes the shop's navigation mesh from the static colliders of the "shop_navigation" group when the scene
///     loads, and again on <see cref="Rebake" /> (for example after a wall, door or furniture changes). The scene's
///     own NavigationMesh holds the bake settings only; the baked mesh is a copy, so no polygons are ever stored in
///     the scene file.
/// </summary>
public partial class ShopNavigationRegion : NavigationRegion3D
{
    private const float AgentRadiusMeters = 0.25f;
    private const float AgentHeightMeters = 1.8f;

    // Equal to the default navigation map cell size (navigation/3d/default_cell_size and default_cell_height), so
    // the baked edges merge with the map without changing any project or map setting.
    private const float BakeCellSize = 0.25f;
    private const float BakeCellHeight = 0.25f;

    // A path to a target that cannot be reached ends at the closest reachable point; that point is farther than this
    // on the XZ plane. Height is ignored: the baked surface is above the markers' y.
    private const float ReachTolerance = 0.5f;

    private NavigationMesh? _settings;

    public override void _Ready()
    {
        _settings = NavigationMesh;
        Rebake();
    }

    public void Rebake()
    {
        var settings = _settings ?? throw new InvalidOperationException(
            "ShopNavigationRegion needs a NavigationMesh in the scene to take its bake settings from.");

        var mesh = (NavigationMesh)settings.Duplicate();
        mesh.AgentRadius = AgentRadiusMeters;
        mesh.AgentHeight = AgentHeightMeters;
        mesh.CellSize = BakeCellSize;
        mesh.CellHeight = BakeCellHeight;

        var source = new NavigationMeshSourceGeometryData3D();
        NavigationServer3D.ParseSourceGeometryData(mesh, source, this);
        NavigationServer3D.BakeFromSourceGeometryData(mesh, source);

        NavigationMesh = mesh;
        NavigationServer3D.MapForceUpdate(GetNavigationMap());
    }

    public bool Reaches(Vector3 from, Vector3 to)
    {
        var path = NavigationServer3D.MapGetPath(GetNavigationMap(), from, to, true);
        if (path.Length == 0)
        {
            return false;
        }

        var end = path[^1];
        return new Vector2(end.X, end.Z).DistanceTo(new Vector2(to.X, to.Z)) <= ReachTolerance;
    }
}
