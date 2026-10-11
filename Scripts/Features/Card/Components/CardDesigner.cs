using System;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Card.Components;

public partial class CardDesigner : Node, ICardComponent
{
    // Default values as constants
    private const float DefaultWidth = 0.635f;
    private const float DefaultHeight = 0.889f;
    private const float DefaultThickness = 0.005f;

    // The card's mesh is baked ahead of time (Data/Card/CardMesh.tres), so these only size the collision shape.
    [Export(PropertyHint.Range, "0.1,3.0,0.01")]
    public float Width { get; set; } = DefaultWidth;

    [Export(PropertyHint.Range, "0.1,3.0,0.01")]
    public float Height { get; set; } = DefaultHeight;

    [Export]
    public float Thickness { get; set; } = DefaultThickness;

    public override bool _PropertyCanRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(Width) => true,
            nameof(Height) => true,
            nameof(Thickness) => true,
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
            _ => base._PropertyGetRevert(property)
        };
    }

    public void Setup(Node cardRoot)
    {
        var collisionShape = cardRoot.GetNodeOrNull<CollisionShape3D>("CardCollision") ??
                             throw new InvalidOperationException("CardCollision node not found");

        var collisionBoxShape = collisionShape.Shape as BoxShape3D ??
                                throw new InvalidOperationException("CardCollision shape is not BoxShape3D");

        collisionBoxShape.Size = new Vector3(Width, Thickness, Height);
    }
}
