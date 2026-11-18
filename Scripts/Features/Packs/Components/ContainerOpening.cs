using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Features.Packs.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Packs.Components;

/// <summary>
///     Plays one container's opening over real time, timed by <see cref="ContainerOpeningPlan" />. Items hang frozen
///     at their spots until the empty wrapper is gone, so none drops back into it.
/// </summary>
internal sealed class ContainerOpening
{
    private const float SmallestScale = 0.01f;
    private const float LongestDrop = 4f;
    private const float RayLift = 0.3f;

    private readonly CardContainer _wrapper;
    private readonly ContainerOpeningPlan _plan;
    private readonly IReadOnlyList<Func<Node3D?>> _spawns;
    private readonly List<RisingItem> _rising = new();
    private readonly List<OpeningPart> _parts;
    private readonly Node3D? _visual;
    private readonly Transform3D _from;
    private readonly Transform3D _to;
    private readonly float _unsealFrom;
    private float _time;

    /// <param name="unsealFrom">
    ///     How far the wrapper's lid or flaps are already open (0 to 1), so a box opened in the hand first does not
    ///     snap shut before it unseals.
    /// </param>
    public ContainerOpening(CardContainer wrapper, Transform3D settled, IReadOnlyList<Func<Node3D?>> spawns,
        float unsealFrom = 0f)
    {
        _wrapper = wrapper;
        _spawns = spawns;
        _from = wrapper.GlobalTransform;
        _to = settled;
        _unsealFrom = unsealFrom;
        _plan = ContainerOpeningPlan.For(wrapper.Kind, _from.Origin.Y - _to.Origin.Y, spawns.Count);
        _visual = wrapper.GetNodeOrNull<Node3D>("Visual");
        _parts = _visual != null ? OpeningPart.Under(_visual) : new List<OpeningPart>();
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

    /// <summary>
    ///     Ends the opening at once: every item still inside appears at its spot and every rising one lands there.
    /// </summary>
    public void Finish()
    {
        _time = _plan.Duration;
        while (Released < _spawns.Count)
            _spawns[Released++]();

        LetAllGo();
        PoseWrapper();
    }

    /// <summary>
    ///     Lets the rising items go where they are, for a wrapper that leaves the tree before it is done.
    /// </summary>
    public void Abandon()
    {
        foreach (var item in _rising)
            if (item.StillOurs())
                item.Unfreeze();

        _rising.Clear();
    }

    public bool IsOver => Released == _spawns.Count && _rising.Count == 0 && _time >= _plan.Duration;

    private void PoseWrapper()
    {
        var fall = _plan.SettleProgress(_time);
        var rotation = _from.Basis.GetRotationQuaternion().Slerp(_to.Basis.GetRotationQuaternion(), fall);
        _wrapper.GlobalTransform = new Transform3D(new Basis(rotation), _from.Origin.Lerp(_to.Origin, fall * fall));

        var unseal = Mathf.Max(_unsealFrom, _plan.UnsealProgress(_time));
        foreach (var part in _parts)
            part.Pose(unseal);

        if (_visual != null)
            _visual.Scale = Vector3.One *
                            Mathf.Max(SmallestScale, 1f - ContainerOpeningPlan.Ease(_plan.VanishProgress(_time)));
    }

    // Creates the next item at its spot, then moves it back inside the wrapper, frozen, to rise from there. At most
    // one item is created a frame, so a slow frame never spawns a whole booster at once.
    private void Release()
    {
        var index = Released++;
        if (_spawns[index]() is not { } node || !node.IsInsideTree())
            return;

        var rising = new RisingItem(node, index, RisePath.For(_wrapper.Kind, index, _to));
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
}
