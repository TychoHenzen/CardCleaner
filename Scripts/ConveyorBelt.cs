// ConveyorTrack.cs
using System;
using System.Collections.Generic;
using Godot;

[Tool]
public partial class ConveyorBelt : Node3D
{
    [Export] public Path3D Rail;               
    [Export] public MeshInstance3D BeltMesh;   
    [Export] public Area3D DetectionArea;      
    [Export] public float Speed { get; set; } = 1.0f;

    private StandardMaterial3D _beltMaterial;
    private readonly List<PathFollow3D> _followers = [];
    private readonly List<RigidBody3D> _cardData = [];

    public override void _Ready()
    {
        if (Rail == null || BeltMesh == null || DetectionArea == null)
        {
            GD.PrintErr("[ConveyorTrack] Assign Rail, BeltMesh, and DetectionArea in the inspector!");
            return;
        }

        // Ensure we have our own material instance to scroll
        if (BeltMesh.MaterialOverride is StandardMaterial3D matOverride)
        {
            _beltMaterial = matOverride;
        }
        else if (BeltMesh.Mesh?.GetSurfaceCount() > 0 &&
                 BeltMesh.Mesh.SurfaceGetMaterial(0) is StandardMaterial3D sharedMat)
        {
            var inst = sharedMat.Duplicate() as StandardMaterial3D;
            BeltMesh.SetSurfaceOverrideMaterial(0, inst);
            _beltMaterial = inst;
        }
        else
        {
            GD.PrintErr("[ConveyorTrack] BeltMesh needs a StandardMaterial3D on surface 0.");
        }

        DetectionArea.Monitoring = true;
        DetectionArea.BodyEntered += (body) => CallDeferred(nameof(HandleEnter), body);
        DetectionArea.BodyExited  += (body) => CallDeferred(nameof(HandleExit),  body);

        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        AnimateBeltUV((float)delta);
        MoveCardsAlongRail((float)delta);
    }

    private void AnimateBeltUV(float dt)
    {
        if (_beltMaterial == null) return;
        var uv = _beltMaterial.Uv1Offset;
        uv.X = (uv.X + Speed * dt) % 1.0f;
        _beltMaterial.Uv1Offset = uv;
    }

    private void MoveCardsAlongRail(float dt)
    {
        float length = Rail.Curve.GetBakedLength();
        float step   = Speed * dt;
        for (int i = _followers.Count - 1; i >= 0; i--)
        {
            var pf = _followers[i];
            if (!IsInstanceValid(pf))
            {
                _followers.RemoveAt(i);
                continue;
            }
            pf.Progress = (pf.Progress + step) % length;
        }
    }

    private void HandleEnter(object arg)
    {
        if (arg is not RigidBody3D card) return;
        if (_cardData.Contains(card)) return;

        // Save its original state
        _cardData.Add(card);

        // Make it static so physics stops applying
        card.Freeze = true;

        // Create the follower
        var pf = new PathFollow3D
        {
            RotationMode = PathFollow3D.RotationModeEnum.Oriented,
            Progress = 0f
        };
        Rail.AddChild(pf);
        _followers.Add(pf);

        // Reparent the card under the follower
        card.Reparent(pf);;
        card.Transform = Transform3D.Identity;
    }

    private void HandleExit(object arg)
    {
        if (arg is not RigidBody3D card) return;

        // Find its follower
        var pf = _followers.Find(p => p.GetChildCount() > 0 && p.GetChild(0) == card);
        if (pf != null)
        {
            pf.RemoveChild(card);
            _followers.Remove(pf);
            pf.QueueFree();
        }

        // Restore physics and transform
        GetParent<Node3D>().AddChild(card);
        card.Freeze = false;

        _cardData.Remove(card);
    }
}
