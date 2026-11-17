using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Features.Packs.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Packs.Components;

/// <summary>
///     Plays one container's opening, timed by <see cref="ContainerOpeningPlan" />: the wrapper falls to the surface
///     below it, its <see cref="OpeningPart" />s open, and its items rise out one by one along a curve from inside it
///     through its mouth to their spot above it. They stay there, frozen, while the empty wrapper shrinks away, and
///     are let go together once it is gone, so none drops back into the wrapper it just left. At most one item is
///     created per frame, so a slow frame never spawns a whole booster at once.
/// </summary>
internal sealed class ContainerOpening
{
    // Where an item starts inside the wrapper and where it leaves it, in the wrapper's own frame. A box and a pack
    // open at the top; a booster's cards slide out of its torn end at -Z.
    private static readonly Vector3 BoxInside = new(0f, 0.01f, 0f);
    private static readonly Vector3 BoxMouth = new(0f, 0.7f, 0f);
    private static readonly Vector3 PackMouth = new(0f, 0.46f, 0f);
    private const float PackFloor = 0.007f;
    private const float BoosterHeight = 0.045f;
    private static readonly Vector3 BoosterInside = new(0f, 0.02f, 0f);
    private static readonly Vector3 BoosterMouth = new(0f, 0.022f, -0.6f);

    private const float SmallestScale = 0.01f;
    private const float LongestDrop = 4f;
    private const float RayLift = 0.3f;

    private readonly CardContainer _wrapper;
    private readonly ContainerOpeningPlan _plan;
    private readonly IReadOnlyList<Func<Node3D?>> _spawns;
    private readonly List<Rising> _rising = new();
    private readonly List<OpeningPart> _parts = new();
    private readonly Node3D? _visual;
    private readonly Transform3D _from;
    private readonly Transform3D _to;
    private float _time;

    public ContainerOpening(CardContainer wrapper, Transform3D settled, IReadOnlyList<Func<Node3D?>> spawns)
    {
        _wrapper = wrapper;
        _spawns = spawns;
        _from = wrapper.GlobalTransform;
        _to = settled;
        _plan = ContainerOpeningPlan.For(wrapper.Kind, _from.Origin.Y - _to.Origin.Y, spawns.Count);
        _visual = wrapper.GetNodeOrNull<Node3D>("Visual");
        if (_visual != null)
            CollectParts(_visual);
    }

    /// <summary>Items that have left the wrapper so far.</summary>
    public int Released { get; private set; }

    /// <summary>Items still inside the wrapper.</summary>
    public int Pending => _spawns.Count - Released;

    /// <summary>
    ///     Where the wrapper comes to rest: on the first surface its collision mask meets straight below it, standing
    ///     upright with its yaw kept, or where it is when nothing is below.
    /// </summary>
    public static Transform3D SettledPose(CardContainer wrapper, uint mask)
    {
        var at = wrapper.GlobalTransform;
        var upright = Basis.FromEuler(new Vector3(0f, at.Basis.GetEuler().Y, 0f));
        var query = new PhysicsRayQueryParameters3D
        {
            From = at.Origin + Vector3.Up * RayLift,
            To = at.Origin + Vector3.Down * LongestDrop,
            CollisionMask = mask,
            Exclude = new Godot.Collections.Array<Rid> { wrapper.GetRid() }
        };
        var hit = wrapper.GetWorld3D().DirectSpaceState.IntersectRay(query);
        var origin = hit.Count > 0 ? hit["position"].AsVector3() : at.Origin;
        return new Transform3D(upright, origin);
    }

    /// <summary>Advances the opening by <paramref name="delta" /> seconds. True once it is over.</summary>
    public bool Advance(float delta)
    {
        _time += delta;
        PoseWrapper();
        if (Released < _spawns.Count && _time >= _plan.ReleaseAt(Released))
            Release();

        MoveRising();
        if (Released == _spawns.Count && _time >= _plan.Duration)
            LetAllGo();

        return IsOver;
    }

    /// <summary>Ends the opening at once: every item still inside appears at its spot and every rising one lands there.</summary>
    public void Finish()
    {
        _time = _plan.Duration;
        while (Released < _spawns.Count)
            _spawns[Released++]();

        LetAllGo();
        PoseWrapper();
    }

    /// <summary>Lets the rising items go where they are, for a wrapper that leaves the tree before it is done.</summary>
    public void Abandon()
    {
        foreach (var item in _rising)
            if (item.StillOurs())
                item.Unfreeze();

        _rising.Clear();
    }

    public bool IsOver => Released == _spawns.Count && _rising.Count == 0 && _time >= _plan.Duration;

    private void CollectParts(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is OpeningPart part)
                _parts.Add(part);
            CollectParts(child);
        }
    }

    private void PoseWrapper()
    {
        var fall = _plan.SettleProgress(_time);
        var rotation = _from.Basis.GetRotationQuaternion().Slerp(_to.Basis.GetRotationQuaternion(), fall);
        _wrapper.GlobalTransform = new Transform3D(new Basis(rotation), _from.Origin.Lerp(_to.Origin, fall * fall));

        var unseal = _plan.UnsealProgress(_time);
        foreach (var part in _parts)
            part.Pose(unseal);

        if (_visual != null)
            _visual.Scale = Vector3.One *
                            Mathf.Max(SmallestScale, 1f - ContainerOpeningPlan.Ease(_plan.VanishProgress(_time)));
    }

    // Creates the next item at its spot, then moves it back inside the wrapper, frozen, to rise from there.
    private void Release()
    {
        var index = Released++;
        if (_spawns[index]() is not { } node || !node.IsInsideTree())
            return;

        var rising = new Rising(node, index, _to * InsideOf(_wrapper.Kind, index), _to * MouthOf(_wrapper.Kind),
            _to.Basis);
        rising.Hold();
        _rising.Add(rising);
    }

    private void MoveRising()
    {
        _rising.RemoveAll(item => !item.StillOurs());
        foreach (var item in _rising)
            item.MoveTo(ContainerOpeningPlan.Ease(_plan.RiseProgress(item.Index, _time)));
    }

    private void LetAllGo()
    {
        foreach (var item in _rising)
            if (item.StillOurs())
                item.LetGo();

        _rising.Clear();
    }

    // A pack's boosters lie stacked flat inside it and leave from the top of the stack down.
    private static Vector3 InsideOf(CardContainerKind kind, int index) => kind switch
    {
        CardContainerKind.Box => BoxInside,
        CardContainerKind.Pack => new Vector3(0f,
            PackFloor + (CardContainerLayout.ItemsPerContainer - 1 - index % CardContainerLayout.ItemsPerContainer) *
            BoosterHeight, 0f),
        _ => BoosterInside
    };

    private static Vector3 MouthOf(CardContainerKind kind) => kind switch
    {
        CardContainerKind.Box => BoxMouth,
        CardContainerKind.Pack => PackMouth,
        _ => BoosterMouth
    };

    /// <summary>
    ///     One item on its way out: a quadratic curve from inside the wrapper, pulled toward its mouth, to the spot it
    ///     was created at, turning from the wrapper's yaw to its own.
    /// </summary>
    private sealed class Rising(Node3D node, int index, Vector3 inside, Vector3 mouth, Basis wrapperBasis)
    {
        private readonly Node? _parent = node.GetParent();
        private readonly Transform3D _target = node.GlobalTransform;

        public int Index { get; } = index;

        // The player may take an item while it rises, or a load may clear it away; then it is no longer ours to move.
        public bool StillOurs() =>
            GodotObject.IsInstanceValid(node) && node.IsInsideTree() && node.GetParent() == _parent;

        public void Hold()
        {
            if (node is RigidBody3D body)
                body.Freeze = true;
            MoveTo(0f);
        }

        public void MoveTo(float t)
        {
            var u = 1f - t;
            var position = u * u * inside + 2f * u * t * mouth + t * t * _target.Origin;
            var rotation = wrapperBasis.GetRotationQuaternion().Slerp(_target.Basis.GetRotationQuaternion(), t);
            node.GlobalTransform = new Transform3D(new Basis(rotation), position);
        }

        public void LetGo()
        {
            node.GlobalTransform = _target;
            Unfreeze();
        }

        public void Unfreeze()
        {
            if (node is RigidBody3D body)
                body.Freeze = false;
        }
    }
}
