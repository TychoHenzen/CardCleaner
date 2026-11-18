using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Card.Components;

public partial class DropPreview : Node3D, IDropPreview
{
    private MeshInstance3D _previewInstance = null!;
    private ImmediateMesh _previewMesh = null!;
    private const float RayLength = 100f;

    // One query for every frame the preview updates, instead of a new finalizable wrapper each frame.
    private PhysicsRayQueryParameters3D? _ray;

    public override void _Ready()
    {
        _previewMesh = new ImmediateMesh();
        _previewInstance = new MeshInstance3D
        {
            Mesh = _previewMesh,
            Visible = false,
            MaterialOverride = new StandardMaterial3D { DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Always },
            Name = "PreviewMesh"
        };
        AddChild(_previewInstance);
    }

    public void ShowPreview(bool show)
    {
        _previewInstance.Visible = show;
    }

    /// <summary>
    ///     Updates the preview mesh to draw a ray from origin forward by rayLength.
    /// </summary>
    public void UpdatePreview(Vector3 origin, Vector3 direction)
    {
        var to = origin + direction * RayLength;
        _ray ??= new PhysicsRayQueryParameters3D { CollideWithBodies = true };
        _ray.From = origin;
        _ray.To = to;
        var result = GetWorld3D().DirectSpaceState.IntersectRay(_ray);

        var hit = result.Count > 0 ? (Vector3)result["position"] : to;

        _previewMesh.ClearSurfaces();
        _previewMesh.SurfaceBegin(Mesh.PrimitiveType.Lines);
        _previewMesh.SurfaceSetColor(Colors.Red);
        _previewMesh.SurfaceAddVertex(ToLocal(origin));
        _previewMesh.SurfaceAddVertex(ToLocal(hit));
        _previewMesh.SurfaceEnd();
    }
}