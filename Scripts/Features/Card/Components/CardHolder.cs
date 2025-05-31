using System.Collections.Generic;
using Godot;

namespace CardCleaner.Scripts.Features.Card.Components;

public partial class CardHolder : Node3D
{
    [Signal]
    public delegate void CardAddedEventHandler(RigidBody3D card);

    [Signal]
    public delegate void CardRemovedEventHandler(RigidBody3D card);

    public readonly List<RigidBody3D> HeldCards = new();
    private Node3D _cardParent;
    private Node3D _handParent;
    [Export] public uint CardCollisionLayer = 2;

    [Export] public float HoldDistance = 2f;

    public int HeldCount => HeldCards.Count;
    public bool HasCards => HeldCards.Count > 0;

    public void SetReferences(Node3D handAnchor, Node3D cardsParent)
    {
        _handParent = handAnchor;
        _cardParent = cardsParent;
    }

    public void AddCard(RigidBody3D card)
    {
        if (HeldCards.Contains(card)) return;

        // Disable physics and collision
        card.Freeze = true;
        var shape = card.GetNode<CollisionShape3D>("CardCollision");
        if (shape != null) shape.Disabled = true;

        // Reparent to hand
        card.Reparent(_handParent);
        HeldCards.Add(card);

        PositionCards();
        EmitSignal(nameof(CardAdded), card);
    }

    public void RemoveCard(RigidBody3D card)
    {
        if (!HeldCards.Contains(card)) return;

        HeldCards.Remove(card);
        EnablePhysics(card);
        card.Reparent(_cardParent);

        PositionCardsForDrop();
        EmitSignal(nameof(CardRemoved), card);
    }

    public void RemoveTopCard()
    {
        if (HeldCards.Count == 0) return;
        var topCard = HeldCards[^1];
        RemoveCard(topCard);
    }

    public void RemoveAllCards()
    {
        while (HeldCards.Count > 0) RemoveCard(HeldCards[^1]);
    }

    public void PositionCards()
    {
        for (var i = 0; i < HeldCards.Count; i++)
        {
            var card = HeldCards[i];
            var designer = card.GetNode<CardDesigner>("Designer");
            var thickness = designer.Thickness;
            var indexOffset = i * thickness;

            var rotation = Basis.Identity.Rotated(Vector3.Right, Mathf.DegToRad(90));
            var localPos = new Vector3(
                1 - indexOffset * 10,
                0,
                -HoldDistance + indexOffset
            );
            card.Transform = new Transform3D(rotation, localPos);
        }
    }

    public void PositionCardsForDrop()
    {
        // Drop-prep: face camera yaw, hold horizontally, stacked up
        var camTransform = _handParent.GlobalTransform;
        var forwardCam = -camTransform.Basis.Z;
        var yaw = Mathf.Atan2(forwardCam.X, forwardCam.Z);
        var faceYaw = Basis.Identity.Rotated(Vector3.Up, yaw + Mathf.DegToRad(180));

        var downOffset = forwardCam * HoldDistance;
        var upAxis = Vector3.Up;

        for (var i = 0; i < HeldCards.Count; i++)
        {
            var card = HeldCards[i];
            var designer = card.GetNode("Designer");
            var thickness = designer.Get("Thickness").AsSingle();
            var worldPos = camTransform.Origin + downOffset + upAxis * (i * thickness);
            card.GlobalTransform = new Transform3D(faceYaw, worldPos);
        }
    }

    private void EnablePhysics(RigidBody3D card)
    {
        card.Freeze = false;
        var shape = card.GetNode<CollisionShape3D>("CardCollision");
        if (shape != null) shape.Disabled = false;
        card.CollisionLayer = CardCollisionLayer;
    }
}