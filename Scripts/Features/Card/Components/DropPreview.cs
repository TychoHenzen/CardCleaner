using Godot;

public partial class DropPreview : Node3D
{
    private ImmediateMesh _previewMesh;
    private MeshInstance3D _previewInstance;
    private float RayLength = 100f;

    public override void _Ready()
    {
        _previewMesh = new ImmediateMesh();
        _previewInstance = new MeshInstance3D
        {
            Mesh = _previewMesh, 
            Visible = false,
            MaterialOverride = new StandardMaterial3D { DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Always }
        };
        AddChild(_previewInstance);
    }

    public void ShowPreview(bool show) => _previewInstance.Visible = show;
 
    /// <summary>
    /// Updates the preview mesh to draw a ray from origin forward by rayLength.
    /// </summary>
    public void UpdatePreview(Vector3 origin, Vector3 direction)
    {
        
        var to = origin + direction * RayLength;
        var result = GetWorld3D().DirectSpaceState.IntersectRay(new PhysicsRayQueryParameters3D {
            From = origin, To = to, CollideWithBodies = true
        });

        var hit = result.Count > 0 ? (Vector3)result["position"] : to;
        
        _previewMesh.ClearSurfaces();
        _previewMesh.SurfaceBegin(Mesh.PrimitiveType.Lines);
        _previewMesh.SurfaceSetColor(Colors.Red);
        _previewMesh.SurfaceAddVertex(origin);
        _previewMesh.SurfaceAddVertex(hit);
        _previewMesh.SurfaceEnd();
    }
}