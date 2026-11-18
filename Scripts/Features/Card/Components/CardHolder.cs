using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Card.Components;

public partial class CardHolder : Node3D, ICardHolder, IDependent
{
    [Signal]
    public delegate void CardAddedEventHandler(RigidBody3D card);

    [Signal]
    public delegate void CardRemovedEventHandler(RigidBody3D card);

    public RigidBody3D[] HeldCards => _heldCards.ToArray();
    private readonly List<RigidBody3D> _heldCards = new();
    private Node3D _handParent = null!;
    private IServiceResolver? _services;

    // Define defaults as constants to use in both field initializers and reversion
    private const uint DefaultCardCollisionLayer = 2;
    private const float DefaultHoldDistance = 2f;
    private const float DefaultFanRadius = 0.8f;
    private const float DefaultFanSpreadDegrees = 50f;
    private const int DefaultFanFillCount = 10;
    private const float DefaultFanDepthStep = 0.006f;
    private static readonly Vector3 DefaultFanCenter = new(0.5f, -0.55f, -1.4f);

    [Export] public uint CardCollisionLayer = DefaultCardCollisionLayer;

    /// <summary>How far in front of the hand anchor the cards lie while a drop is being aimed.</summary>
    [Export] public float HoldDistance = DefaultHoldDistance;

    /// <summary>Where the middle of the held fan sits, in the hand anchor's frame.</summary>
    [Export] public Vector3 FanCenter = DefaultFanCenter;

    /// <summary>Distance from the point the fan turns about (straight below the middle card) to each card's centre.</summary>
    [Export] public float FanRadius = DefaultFanRadius;

    /// <summary>Angle between the outermost cards once the hand holds <see cref="FanFillCount" /> cards or more.</summary>
    [Export] public float FanSpreadDegrees = DefaultFanSpreadDegrees;

    /// <summary>How many cards it takes to fill the spread (see <see cref="HandFan" />).</summary>
    [Export] public int FanFillCount = DefaultFanFillCount;

    /// <summary>How much nearer the camera each next card lies, so the cards never sink into each other.</summary>
    [Export] public float FanDepthStep = DefaultFanDepthStep;

    public override bool _PropertyCanRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(CardCollisionLayer) => true,
            nameof(HoldDistance) => true,
            nameof(FanCenter) => true,
            nameof(FanRadius) => true,
            nameof(FanSpreadDegrees) => true,
            nameof(FanFillCount) => true,
            nameof(FanDepthStep) => true,
            _ => base._PropertyCanRevert(property)
        };
    }

    public override Variant _PropertyGetRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(CardCollisionLayer) => DefaultCardCollisionLayer,
            nameof(HoldDistance) => DefaultHoldDistance,
            nameof(FanCenter) => DefaultFanCenter,
            nameof(FanRadius) => DefaultFanRadius,
            nameof(FanSpreadDegrees) => DefaultFanSpreadDegrees,
            nameof(FanFillCount) => DefaultFanFillCount,
            nameof(FanDepthStep) => DefaultFanDepthStep,
            _ => base._PropertyGetRevert(property)
        };
    }

    public int HeldCount => _heldCards.Count;
    public bool HasCards => _heldCards.Count > 0;

    /// <summary>The card a single drop or a stock click takes first, or null when the hand is empty.</summary>
    public RigidBody3D? TopCard => _heldCards.Count > 0 ? _heldCards[^1] : null;

    public void Inject(IServiceResolver services) => _services = services;

    public void SetReferences(Node3D handAnchor)
    {
        _handParent = handAnchor;
    }

    public void AddCard(RigidBody3D card)
    {
        if (_heldCards.Contains(card)) return;

        // Disable physics and collision
        card.Freeze = true;
        var shape = card.GetNode<CollisionShape3D>("CardCollision");
        if (shape != null) shape.Disabled = true;

        // Reparent to hand
        card.Reparent(_handParent);
        _heldCards.Add(card);

        // Emit pickup signal AFTER successful reparenting
        if (card is CardController cardController) cardController.EmitPickupSignal();

        PositionCards();
        EmitSignal(SignalName.CardAdded, card);
    }

    public void RemoveCard(RigidBody3D card)
    {
        if (!_heldCards.Contains(card)) return;

        // Removed before the injection check, so RemoveAllCards still ends when the holder was not injected.
        _heldCards.Remove(card);
        if (_services == null)
        {
            ILog.Error($"{Name} was not injected");
            return;
        }

        // Reparent first, then enable physics with a delay to avoid conflicts
        card.Reparent(_services.Get<ICardSpawner>().GetNode());
        CallDeferred(MethodName.EnablePhysicsDeferred, card);

        PositionCardsForDrop();
        EmitSignal(SignalName.CardRemoved, card);
    }

    /// <summary>
    ///     Gives a held card to a holder that locks it at once, such as a shelf slot. Unlike <see cref="RemoveCard" />
    ///     it stays frozen where it is, with its collision back on: the new holder reparents and places it, and no
    ///     deferred unfreeze can drop it out of that holder. False when the hand does not hold the card.
    /// </summary>
    public bool HandOver(RigidBody3D card)
    {
        if (!_heldCards.Remove(card)) return false;

        var shape = card.GetNodeOrNull<CollisionShape3D>("CardCollision");
        if (shape != null) shape.Disabled = false;
        card.CollisionLayer = CardCollisionLayer;

        PositionCards();
        EmitSignal(SignalName.CardRemoved, card);
        return true;
    }

    private void EnablePhysicsDeferred(RigidBody3D card)
    {
        if (IsInstanceValid(card)) EnablePhysics(card);
    }

    public void RemoveTopCard()
    {
        if (_heldCards.Count == 0) return;
        var topCard = _heldCards[^1];
        RemoveCard(topCard);
    }

    public void RemoveAllCards()
    {
        while (_heldCards.Count > 0) RemoveCard(_heldCards[^1]);
    }

    /// <summary>Lays the held cards out as a fan in front of the hand anchor (see <see cref="FanPose" />).</summary>
    public void PositionCards()
    {
        for (var i = 0; i < _heldCards.Count; i++)
            _heldCards[i].Transform = FanPose(i, _heldCards.Count);
    }

    /// <summary>
    ///     The pose of the card at <paramref name="index" /> (0 is the leftmost) in a hand of <paramref name="count" />
    ///     cards, in the hand anchor's frame. Each card faces the camera, turned about the point
    ///     <see cref="FanRadius" /> below <see cref="FanCenter" /> by its <see cref="HandFan" /> angle, so its top leans
    ///     out; each next card lies <see cref="FanDepthStep" /> nearer the camera, on top of the one before.
    /// </summary>
    public Transform3D FanPose(int index, int count)
    {
        var angle = Mathf.DegToRad(HandFan.AngleDegrees(index, count, FanSpreadDegrees, FanFillCount));
        var pivot = FanCenter + Vector3.Down * FanRadius;
        var position = pivot + new Vector3(Mathf.Sin(angle), Mathf.Cos(angle), 0f) * FanRadius
                       + Vector3.Back * (index * FanDepthStep);

        // Standing the card up (90 degrees about X) shows its face to the camera with its top up; the roll about the
        // view axis then leans a card right of the middle clockwise.
        var basis = new Basis(Vector3.Back, -angle) * new Basis(Vector3.Right, Mathf.Pi / 2f);
        return new Transform3D(basis, position);
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

        for (var i = 0; i < _heldCards.Count; i++)
        {
            var card = _heldCards[i];
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