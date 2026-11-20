using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Packs.Components;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     What the restocker holds: at most one container, parked on its Hand anchor. A container is taken from a rack
///     level or the floor, then either locked onto a shelf level or set down in the world. It moves as a body, not as
///     a record, so a cardboard box keeps its contents and a container its seed and price.
/// </summary>
internal sealed class RestockerHands
{
    private static readonly Vector3 SetDownOffset = new(0f, 0.3f, 0f);

    private readonly Node3D _owner;
    private readonly Node3D _hand;
    private CardContainer? _carried;
    private uint _savedLayer;

    public RestockerHands(Node3D owner, Node3D hand)
    {
        _owner = owner;
        _hand = hand;
    }

    /// <summary>The container on the Hand, or null when the hands are empty.</summary>
    public CardContainer? Carried =>
        _carried != null && GodotObject.IsInstanceValid(_carried) ? _carried : null;

    /// <summary>The level the carried container was taken from, or null when it came off the floor.</summary>
    public ShelfLevel? Origin { get; private set; }

    /// <summary>Takes the container (freeing its seat). True when it is in the hands afterwards.</summary>
    public bool Take(CardContainer container)
    {
        if (ReferenceEquals(Carried, container))
            return true;
        if (Carried != null || !GodotObject.IsInstanceValid(container) || container.IsOpened || container.IsHeld)
            return false;

        var seat = container.GetParent() as DeckSlot;
        Origin = seat?.GetParent() as ShelfLevel;
        seat?.ReleaseCard(container);

        // Off every collision layer while carried: the Area of the slot it left would otherwise see the reparented
        // body enter again and lock it straight back.
        _savedLayer = container.CollisionLayer;
        container.CollisionLayer = 0;
        container.Freeze = true;
        container.LinearVelocity = Vector3.Zero;
        container.AngularVelocity = Vector3.Zero;
        container.Reparent(_hand, false);
        container.Transform = Transform3D.Identity;
        _carried = container;
        return true;
    }

    /// <summary>Locks the carried container onto the level. False, still carried, when the level refuses.</summary>
    public bool Shelve(ShelfLevel level)
    {
        if (Carried is not { } container)
            return false;

        container.CollisionLayer = _savedLayer;
        if (level.TryLock(container))
        {
            _carried = null;
            return true;
        }

        container.CollisionLayer = 0;
        return false;
    }

    /// <summary>Sets the carried container down at the restocker's feet and returns it, or null when empty.</summary>
    public CardContainer? SetDown()
    {
        if (Carried is not { } container || _owner.GetParent() is not { } world)
            return null;

        container.CollisionLayer = _savedLayer;
        container.Reparent(world, true);
        container.GlobalPosition = _owner.GlobalPosition + SetDownOffset;
        container.Freeze = false;
        _carried = null;
        return container;
    }
}
