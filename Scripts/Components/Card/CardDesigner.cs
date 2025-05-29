using System.Linq;
using CardCleaner.Scripts.Interfaces;
using Godot;

namespace CardCleaner.Scripts;

[Tool]
public partial class CardDesigner : Node, ICardComponent
{
    private float _width = 0.635f;
    private float _height = 0.889f;
    private float _thickness = 0.005f;

    [Export(PropertyHint.Range, "0.1,3.0,0.01")]
    public float Width
    {
        get => _width;
        set
        {
            if (Mathf.IsEqualApprox(_width, value)) return;
            _width = value;
            UpdateShape();
        }
    }

    [Export(PropertyHint.Range, "0.1,3.0,0.01")]
    public float Height
    {
        get => _height;
        set
        {
            if (Mathf.IsEqualApprox(_height, value)) return;
            _height = value;
            UpdateShape();
        }
    }

    [Export]
    public float Thickness
    {
        get => _thickness;
        set
        {
            if (Mathf.IsEqualApprox(_thickness, value)) return;
            _thickness = value;
            UpdateShape();
        }
    }

    [Export(PropertyHint.Range, "0.0,1.0,0.001")]
    public float BevelSize { get; set; } = 0.032f;

    [Export] public int BevelSides { get; set; } = 16;
    [Export] public float OutlineMargin { get; set; } = 0.01f;

    private const float InnerThicknessOffset = 0.002f;

    private CsgBox3D _outerBox;
    private CsgCombiner3D _combiner;
    private CsgCylinder3D[] _cornerCylinders;
    private CsgBox3D[] _trimBoxes;
    private CollisionShape3D _collisionShape;
    private BoxShape3D _collisionBoxShape;
    private CsgBox3D _outlineBox;

    public void Setup(RigidBody3D cardRoot)
    {
        _outerBox = cardRoot.GetNode<CsgBox3D>("OuterBox");
        _combiner = _outerBox.GetNode<CsgCombiner3D>("Combiner");

        _cornerCylinders = _combiner.GetChildren().OfType<CsgCylinder3D>().ToArray();
        _trimBoxes = _combiner.GetChildren().OfType<CsgBox3D>().ToArray();

        _collisionShape = cardRoot.GetNode<CollisionShape3D>("CardCollision");
        _collisionBoxShape = _collisionShape.Shape as BoxShape3D;

        _outlineBox = cardRoot.GetNode<CsgBox3D>("OutlineBox");
        _outlineBox.Visible = false;

        UpdateShape();
    }

    public void IntegrateForces(PhysicsDirectBodyState3D state)
    {
    }

    private void UpdateShape()
    {
        if (_outerBox == null) return;

        _outerBox.Size = new Vector3(Width, Thickness, Height);

        float halfW = Width * 0.5f - BevelSize;
        float halfH = Height * 0.5f - BevelSize;

        foreach (var cyl in _cornerCylinders)
        {
            cyl.Radius = BevelSize;
            cyl.Height = Thickness;
            cyl.Sides = BevelSides;

            bool negX = cyl.Name.ToString().EndsWith("2") || cyl.Name.ToString().EndsWith("3");
            bool negZ = cyl.Name.ToString().EndsWith("3") || cyl.Name.ToString().EndsWith("4");
            var t = cyl.Transform;
            t.Origin = new Vector3(negX ? -halfW : halfW, 0, negZ ? halfH : -halfH);
            cyl.Transform = t;
        }

        if (_trimBoxes.Length >= 2)
        {
            _trimBoxes[0].Size = new Vector3(Width, Thickness + InnerThicknessOffset, Height - 2 * BevelSize);
            _trimBoxes[1].Size = new Vector3(Width - 2 * BevelSize, Thickness + InnerThicknessOffset, Height);
        }

        _collisionBoxShape.Size = new Vector3(Width, Thickness, Height);

        if (_outlineBox != null)
        {
            _outlineBox.Size = new Vector3(Width + OutlineMargin, Thickness + OutlineMargin, Height + OutlineMargin);
        }
    }
}