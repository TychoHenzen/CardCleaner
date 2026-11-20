using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Utilities;
using CardCleaner.Scripts.Features.Packs.Components;
using Godot;

namespace CardCleaner.Scripts.Features.Hand.Components;

/// <summary>
///     How a body goes into the hand and out of it again. A held body is out of play: frozen under the hand anchor and
///     on no collision layer, so no slot trigger it still overlaps takes it back (<c>DeckSlot.IsFreeStock</c>).
///     Leaving, a container gets back the layers it had, and a card the card layer.
/// </summary>
internal sealed class HeldBodies
{
    // How far away, and how level, the spot an item out of an open box is set down on may be: arm's reach, and a
    // surface no steeper than about 45 degrees. The item is set down a little above the spot and falls onto it.
    private const float PutOneDownReach = 4.5f;
    private const float LevelEnough = 0.7f;
    private const float PutDownLift = 0.05f;
    private const uint WorldLayer = 1;

    // The collision a container had before the hand took it, so setting it down restores exactly that.
    private readonly Dictionary<Node3D, (uint Layer, uint Mask)> _collision = new();

    // The layer a card gets back, read when it leaves (the hand's export can change after construction).
    private readonly Func<uint> _cardLayer;

    public HeldBodies(Func<uint> cardLayer) => _cardLayer = cardLayer;

    /// <summary>Takes <paramref name="body" /> out of play under <paramref name="anchor" />.</summary>
    public void Hold(RigidBody3D body, Node3D anchor)
    {
        if (body is CardContainer container)
        {
            _collision[body] = (container.CollisionLayer, container.CollisionMask);
            container.CollisionMask = 0;
            container.ClearHighlight();
        }
        else if (body.GetNodeOrNull<CollisionShape3D>("CardCollision") is { } shape)
        {
            shape.Disabled = true;
        }

        body.CollisionLayer = 0;
        body.Freeze = true;
        if (body.GetParent() != anchor)
            body.Reparent(anchor);
    }

    /// <summary>
    ///     Moves a body under <paramref name="world" />, still frozen where it hangs, then gives its collision back. In
    ///     that order: a collidable body reparented out of the camera re-enters the physics space at its hand pose,
    ///     inside the player's capsule, and pushes the player (a stocked booster shoved the player 0.36 m).
    /// </summary>
    public void PlaceIn(Node3D body, Node world)
    {
        body.Reparent(world);
        Restore(body);
    }

    /// <summary>Sets a body down free at <paramref name="at" /> under <paramref name="world" />.</summary>
    public void SetDown(RigidBody3D body, Node world, Vector3 at)
    {
        body.Reparent(world);
        body.GlobalPosition = at;
        Restore(body);
        body.Freeze = false;
    }

    /// <summary>
    ///     Lets a card fall where it hangs. Its physics comes back a frame after it moved under
    ///     <paramref name="world" />, so it does not meet the hand's other cards.
    /// </summary>
    public void Drop(RigidBody3D card, Node world)
    {
        card.Reparent(world);
        Callable.From(() =>
        {
            if (!GodotObject.IsInstanceValid(card))
                return;
            card.Freeze = false;
            Restore(card);
        }).CallDeferred();
    }

    /// <summary>Frees a body the hand let go of (it left as a record, or was thrown away).</summary>
    public void Free(Node3D body)
    {
        Forget(body);
        body.GetParent()?.RemoveChild(body);
        body.QueueFree();
    }

    /// <summary>Drops what is kept for a body that left the hand without the hand letting go.</summary>
    public void Forget(Node3D body) => _collision.Remove(body);

    /// <summary>
    ///     Where an item out of an open box goes down: the level surface the eye looks at within arm's reach, turned to
    ///     the player's heading so it lies square to the view; null when there is none.
    /// </summary>
    public static Transform3D? PutDownSpot(Camera3D eye)
    {
        var from = eye.GlobalPosition;
        var hit = eye.GetWorld3D().DirectSpaceState.IntersectRay(new PhysicsRayQueryParameters3D
        {
            From = from,
            To = from - eye.GlobalBasis.Z * PutOneDownReach,
            CollisionMask = WorldLayer,
            Exclude = CameraRay.OwnBodies(eye),
        });
        if (hit.Count == 0 || hit["normal"].AsVector3().Y < LevelEnough)
            return null;

        var heading = new Basis(Vector3.Up, eye.GlobalRotation.Y);
        return new Transform3D(heading, hit["position"].AsVector3() + Vector3.Up * PutDownLift);
    }

    private void Restore(Node3D body)
    {
        if (body is CardContainer container)
        {
            var (layer, mask) = _collision.TryGetValue(body, out var saved) ? saved : (1u, 1u);
            _collision.Remove(body);
            container.CollisionLayer = layer;
            container.CollisionMask = mask;
        }
        else if (body is RigidBody3D card)
        {
            if (card.GetNodeOrNull<CollisionShape3D>("CardCollision") is { } shape)
                shape.Disabled = false;
            card.CollisionLayer = _cardLayer();
        }
    }
}
