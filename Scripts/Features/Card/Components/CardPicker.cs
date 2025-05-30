using Godot;

namespace CardCleaner.Scripts.Features.Card.Components;

public partial class CardPicker : Node3D
{
    [Signal]
    public delegate void CardDetectedEventHandler(RigidBody3D card);

    [Signal]
    public delegate void NoCardDetectedEventHandler();

    [Export] public Camera3D Camera;
    public uint CollisionMask = 2;

    public float RayLength = 100f;

    public override void _PhysicsProcess(double delta)
    {
        var origin = Camera.GlobalTransform.Origin;
        var forward = -Camera.GlobalTransform.Basis.Z;
        var result = GetWorld3D().DirectSpaceState.IntersectRay(new PhysicsRayQueryParameters3D
        {
            From = origin,
            To = origin + forward * RayLength,
            CollideWithBodies = true,
            CollideWithAreas = false,
            CollisionMask = CollisionMask
        });
        if (result.Count > 0
            && result["collider"].Obj is RigidBody3D rb
            && rb.Name.ToString().StartsWith("Card"))
            EmitSignal(nameof(CardDetected), rb);
        else
            EmitSignal(nameof(NoCardDetected));
    }
}