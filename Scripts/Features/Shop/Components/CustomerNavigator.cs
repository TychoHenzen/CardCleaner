using System;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     Walks a body along the baked navigation mesh toward one target at a time. It calls back once when the body
///     arrives and once when it can make no progress.
/// </summary>
internal sealed class CustomerNavigator
{
    private const float ArriveMeters = 0.6f;
    private const float ProgressMeters = 0.1f;
    private const int PathSettleFrames = 3;

    // ASSUMPTION: an explicit constructor, not a primary one: the quality gate's function-length rule measures a
    // primary-constructor class as one function spanning its whole body (71 lines here), so it blocks the PR.
    private readonly CharacterBody3D _body;
    private readonly NavigationAgent3D _agent;
    private readonly Action _arrived;
    private readonly Action _blocked;
    private Vector3 _target;
    private Vector3 _anchor;
    private int _framesSinceTarget;
    private float _stuckElapsed;

    public CustomerNavigator(CharacterBody3D body, NavigationAgent3D agent, Action arrived, Action blocked)
    {
        _body = body;
        _agent = agent;
        _arrived = arrived;
        _blocked = blocked;
    }

    /// <summary>Starts a walk to the point on the mesh nearest <paramref name="point" />.</summary>
    public void Start(Vector3 point)
    {
        _target = OnMesh(point);
        // The baked surface lies above the customer's origin (surface y 0.5, origin y 0), and the agent compares
        // waypoints to the origin in 3D. The offset puts the agent's waypoints at the origin's height.
        _agent.PathHeightOffset = OnMesh(_body.GlobalPosition).Y - _body.GlobalPosition.Y;
        _agent.TargetPosition = _target;
        _framesSinceTarget = 0;
        _anchor = _body.GlobalPosition;
        _stuckElapsed = 0f;
    }

    /// <summary>One physics step of the current walk; it raises at most one callback and then returns.</summary>
    public void Step(double delta, float speed, float stuckSeconds)
    {
        _framesSinceTarget++;
        var next = _agent.GetNextPathPosition();
        if (Flat(_target - _body.GlobalPosition).Length() <= ArriveMeters)
        {
            _arrived();
            return;
        }

        if (_framesSinceTarget > PathSettleFrames && _agent.IsNavigationFinished())
        {
            _blocked();
            return;
        }

        var heading = Flat(next - _body.GlobalPosition);
        _body.Velocity = heading.LengthSquared() > 0.0001f
            ? new Vector3(heading.Normalized().X, 0f, heading.Normalized().Y) * speed
            : Vector3.Zero;
        _body.MoveAndSlide();
        TrackProgress(delta, stuckSeconds);
    }

    private static Vector2 Flat(Vector3 v) => new(v.X, v.Z);

    private Vector3 OnMesh(Vector3 point) => NavigationServer3D.MapGetClosestPoint(_agent.GetNavigationMap(), point);

    private void TrackProgress(double delta, float stuckSeconds)
    {
        _stuckElapsed += (float)delta;
        if (Flat(_body.GlobalPosition - _anchor).Length() >= ProgressMeters)
        {
            _anchor = _body.GlobalPosition;
            _stuckElapsed = 0f;
        }
        else if (_stuckElapsed >= stuckSeconds)
        {
            _blocked();
        }
    }
}
