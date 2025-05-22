using Godot;
using System.Collections.Generic;

[Tool]
public partial class CardDesigner : Node3D
{
    // Card dimensions
    [Export] public float Width = 0.635f;
    [Export] public float Height = 0.889f;
    [Export] public float Thickness = 0.005f;

    // Bevel parameters
    [Export(PropertyHint.Range, "0.0,1.0,0.001")] public float BevelSize = 0.032f;
    [Export] public int BevelSides = 16;

    // Small extra penetration for inner cuts
    private const float InnerThicknessOffset = 0.002f;

    private CsgBox3D _outerBox;
    private CsgCombiner3D _combiner;
    private CsgCylinder3D[] _cornerCylinders;
    private CsgBox3D[] _trimBoxes;
    private CollisionShape3D _collisionShape;
    private BoxShape3D _collisionBoxShape;

    public override void _Ready()
    {
        // Grab references
        _outerBox = GetNode<CsgBox3D>("CSGBox3D");
        _combiner = _outerBox.GetNode<CsgCombiner3D>("CSGCombiner3D");

        // Collect all CSGCylinder3D children (the corners)
        var cylList = new List<CsgCylinder3D>();
        foreach (var child in _combiner.GetChildren())
            if (child is CsgCylinder3D c) cylList.Add(c);
        _cornerCylinders = cylList.ToArray();

        // Collect the two inner CSGBox3D children (edge trims)
        var boxList = new List<CsgBox3D>();
        foreach (var child in _combiner.GetChildren())
            if (child is CsgBox3D b) boxList.Add(b);
        _trimBoxes = boxList.ToArray();

        // Collision shape
        _collisionShape = GetNode<CollisionShape3D>("CollisionShape3D");
        _collisionBoxShape = _collisionShape.Shape as BoxShape3D;

        UpdateShape();
    }

    public override void _Process(double delta)
    {
        // In the editor, keep shapes in sync as you tweak exports
        if (Engine.IsEditorHint())
            UpdateShape();
    }

    private void UpdateShape()
    {
        // 1) Outer card
        _outerBox.Size = new Vector3(Width, Thickness, Height);

        // 2) Bevel corner cylinders
        float halfW = Width * 0.5f - BevelSize;
        float halfH = Height * 0.5f - BevelSize;
        foreach (var cyl in _cornerCylinders)
        {
            cyl.Radius = BevelSize;
            cyl.Height = Thickness;
            cyl.Sides = BevelSides;

            // Determine X/Z sign by node name suffix
            float x = (cyl.Name.ToString().EndsWith("2") || cyl.Name.ToString().EndsWith("3")) ? -halfW : halfW;
            float z = (cyl.Name.ToString().EndsWith("3") || cyl.Name.ToString().EndsWith("4")) ? halfH : -halfH;
            var t = cyl.Transform;
            t.Origin = new Vector3(x, 0, z);
            cyl.Transform = t;
        }

        // 3) Trim boxes for straight edges
        if (_trimBoxes.Length >= 2)
        {
            // First box: full width, cut height
            _trimBoxes[0].Size = new Vector3(
                Width,
                Thickness + InnerThicknessOffset,
                Height - 2 * BevelSize
            );
            // Second box: cut width, full height
            _trimBoxes[1].Size = new Vector3(
                Width - 2 * BevelSize,
                Thickness + InnerThicknessOffset,
                Height
            );
        }

        // 4) Match collision shape to outer card
        if (_collisionBoxShape != null)
            _collisionBoxShape.Size = new Vector3(Width, Thickness, Height);
    }
}
