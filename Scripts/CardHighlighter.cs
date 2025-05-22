using Godot;

public partial class CardHighlighter : Node3D
{
    [Export] public NodePath CameraPath;
    [Export] public float RayLength = 100f;

    private Camera3D _camera;
    private RigidBody3D _lastCard;
    private Material _standardMat;
    private ShaderMaterial _outlineMat = GD.Load<ShaderMaterial>("res://Shaders/OutlineMaterial.tres");

    public override void _Ready()
    {
        _camera = GetNode<Camera3D>(CameraPath);
        // Assume all cards share the same standard material:
        _standardMat = GD.Load<Material>("res://Materials/StandardMaterial.tres");
    }

    public override void _PhysicsProcess(double delta)
    {
        Vector3 from = _camera.GlobalTransform.Origin;
        Vector3 to = from + -_camera.GlobalTransform.Basis.Z * RayLength;

        var space = GetWorld3D().DirectSpaceState;
        var queryParameters = new PhysicsRayQueryParameters3D();
        queryParameters.From = from;
        queryParameters.To = to;
        var result = space.IntersectRay(queryParameters);

        if (result.ContainsKey("collider"))
        {
            var card = result["collider"].As<RigidBody3D>();
            if (result.Count > 0 && card != null && card.GetParent().Name.ToString().StartsWith("card"))
            {
                if (_lastCard == card) return;

                ClearOutline();
                ApplyOutline(card);
            }
            else
            {
                ClearOutline();
            }
        }
    }

    private void ApplyOutline(RigidBody3D card)
    {
        // We assume the visible mesh is the CSGBox3D child:
        var mesh = card.GetNode<CsgBox3D>("CSGBox3D");
        mesh.Material = _outlineMat;
        _lastCard = card;
    }

    private void ClearOutline()
    {
        if (_lastCard == null) return;

        var mesh = _lastCard.GetNode<CsgBox3D>("CSGBox3D");
        mesh.Material = _standardMat;
        _lastCard = null;
    }
}