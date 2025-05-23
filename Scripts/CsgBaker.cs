using Godot;
using CardCleaner.Scripts.Interfaces;

[Tool]
public partial class CsgBaker : CsgBox3D, ICardComponent
{
    [Export] public bool BakeOnSetup = true;
    private bool _baked = false;

    public void Setup(RigidBody3D cardRoot)
    {
        if (_baked || !BakeOnSetup)
            return;
        // Defer the actual bake to idle time
        CallDeferred(nameof(PerformDeferredBake), cardRoot);
    }

    public void IntegrateForces(PhysicsDirectBodyState3D state)
    {
        // no-op
    }

    // Runs in the next idle frame
    private void PerformDeferredBake(RigidBody3D cardRoot)
    {
        if (_baked)
            return;
        BakeToMesh(cardRoot);
        _baked = true;
    }

    private void BakeToMesh(RigidBody3D cardRoot)
    {
        // Find the top‐most CSG node
        CsgShape3D rootCsg = this;
        while (rootCsg.GetParent() is CsgShape3D parentCsg)
            rootCsg = parentCsg;

        var bakedMesh = rootCsg.BakeStaticMesh();
        if (bakedMesh == null)
        {
            GD.PrintErr($"CSGBaker: BakeStaticMesh returned null on '{rootCsg.Name}'");
            return;
        }

        var meshInstance = new MeshInstance3D
        {
            Name = $"{rootCsg.Name}_Baked",
            Mesh = bakedMesh,
            MaterialOverride = MaterialOverride
        };
        cardRoot.AddChild(meshInstance);

        rootCsg.Visible = false;
    }
}