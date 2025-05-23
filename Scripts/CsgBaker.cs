using Godot;

namespace CardCleaner.Scripts;

[Tool]
public partial class CSGBaker : CsgBox3D {
    [Export] public bool BakeOnReady = true;

    private bool _baked = false;

    public override void _Ready() {
        // Only bake once, and optionally skip baking in the editor:
        if (_baked || (Engine.IsEditorHint() && !BakeOnReady)) {
            return;
        }
        BakeToMesh();
        _baked = true;
    }

    private void BakeToMesh() {
        // 1. Bake the CSG shape into a Mesh resource
        var bakedMesh = BakeStaticMesh();
        if (bakedMesh == null) {
            GD.PrintErr("CSGBaker: BakeStaticMesh returned null");
            return;
        }

        // 2. Create a MeshInstance3D sibling
        var meshInstance = new MeshInstance3D {
            Name = Name + "_Baked",
            Mesh = bakedMesh,
            MaterialOverride = MaterialOverride // copy your ShaderMaterial
        };
        GetParent().AddChild(meshInstance);

        // 3. Hide the original CSG so they don't overlap
        Visible = false;
    }
}