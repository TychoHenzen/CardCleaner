using System;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Card.Components;

public partial class CardDesigner : Node, ICardComponent
{
    private const float InnerThicknessOffset = 0.002f;

    // Default values as constants
    private const float DefaultWidth = 0.635f;
    private const float DefaultHeight = 0.889f;
    private const float DefaultThickness = 0.005f;
    private const float DefaultBevelSize = 0.032f;
    private const int DefaultBevelSides = 16;
    private const float DefaultOutlineMargin = 0.01f;

    private BoxShape3D? _collisionBoxShape;
    private CollisionShape3D? _collisionShape;
    private CsgCombiner3D? _combiner;
    private CsgCylinder3D[] _cornerCylinders = Array.Empty<CsgCylinder3D>();
    private float _height = DefaultHeight;
    private CsgBox3D? _outerBox;
    private CsgBox3D? _outlineBox;
    private float _thickness = DefaultThickness;
    private CsgBox3D[] _trimBoxes = Array.Empty<CsgBox3D>();
    private float _width = DefaultWidth;

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
    public float BevelSize { get; set; } = DefaultBevelSize;

    [Export] public int BevelSides { get; set; } = DefaultBevelSides;
    [Export] public float OutlineMargin { get; set; } = DefaultOutlineMargin;

    public override bool _PropertyCanRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(Width) => true,
            nameof(Height) => true,
            nameof(Thickness) => true,
            nameof(BevelSize) => true,
            nameof(BevelSides) => true,
            nameof(OutlineMargin) => true,
            _ => base._PropertyCanRevert(property)
        };
    }

    public override Variant _PropertyGetRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(Width) => DefaultWidth,
            nameof(Height) => DefaultHeight,
            nameof(Thickness) => DefaultThickness,
            nameof(BevelSize) => DefaultBevelSize,
            nameof(BevelSides) => DefaultBevelSides,
            nameof(OutlineMargin) => DefaultOutlineMargin,
            _ => base._PropertyGetRevert(property)
        };
    }

    public void Setup(Node cardRoot)
    {
        _outerBox = cardRoot.GetNodeOrNull<CsgBox3D>("OuterBox") ??
                    throw new InvalidOperationException("OuterBox node not found");

        _combiner = _outerBox.GetNodeOrNull<CsgCombiner3D>("Combiner") ??
                    throw new InvalidOperationException("Combiner node not found");

        _cornerCylinders = _combiner.GetChildren().OfType<CsgCylinder3D>().ToArray();
        _trimBoxes = _combiner.GetChildren().OfType<CsgBox3D>().ToArray();

        _collisionShape = cardRoot.GetNodeOrNull<CollisionShape3D>("CardCollision") ??
                          throw new InvalidOperationException("CardCollision node not found");

        _collisionBoxShape = _collisionShape.Shape as BoxShape3D ??
                             throw new InvalidOperationException("CardCollision shape is not BoxShape3D");

        _outlineBox = cardRoot.GetNodeOrNull<CsgBox3D>("OutlineBox");
        if (_outlineBox != null)
            _outlineBox.Visible = false;

        UpdateShape();
    }

    private void UpdateShape()
    {
        if (_outerBox == null || _combiner == null || _collisionBoxShape == null) return;

        _outerBox.Size = new Vector3(Width, Thickness, Height);

        var halfW = Width * 0.5f - BevelSize;
        var halfH = Height * 0.5f - BevelSize;

        foreach (var cyl in _cornerCylinders)
        {
            cyl.Radius = BevelSize;
            cyl.Height = Thickness;
            cyl.Sides = BevelSides;

            var negX = cyl.Name.ToString().EndsWith('2') || cyl.Name.ToString().EndsWith('3');
            var negZ = cyl.Name.ToString().EndsWith('3') || cyl.Name.ToString().EndsWith('4');
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
            _outlineBox.Size = new Vector3(Width + OutlineMargin, Thickness + OutlineMargin, Height + OutlineMargin);
    }
}