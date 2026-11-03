using System;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     Walks a body along the baked navigation mesh toward one target at a time. It calls back once when the body
///     arrives and once when it can make no progress.
/// </summary>
internal sealed class CustomerNavigator(
    CharacterBody3D body,
    NavigationAgent3D agent,
    Action arrived,
    Action blocked)
{
    private const float ArriveMeters = 0.6f;
    private const float ProgressMeters = 0.1f;
    private const int PathSettleFrames = 3;

    private Vector3 _target;
    private Vector3 _anchor;
    private int _framesSinceTarget;
    private float _stuckElapsed;

    /// <summary>Starts a walk to the point on the mesh nearest <paramref name="point" />.</summary>
    public void Start(Vector3 point)
    {
        _target = OnMesh(point);
        // The baked surface lies above the customer's origin (surface y 0.5, origin y 0), and the agent compares
        // waypoints to the origin in 3D. The offset puts the agent's waypoints at the origin's height.
        agent.PathHeightOffset = OnMesh(body.GlobalPosition).Y - body.GlobalPosition.Y;
        agent.TargetPosition = _target;
        _framesSinceTarget = 0;
        _anchor = body.GlobalPosition;
        _stuckElapsed = 0f;
    }

    /// <summary>One physics step of the current walk; it raises at most one callback and then returns.</summary>
    public void Step(double delta, float speed, float stuckSeconds)
    {
        _framesSinceTarget++;
        var next = agent.GetNextPathPosition();
        if (Flat(_target - body.GlobalPosition).Length() <= ArriveMeters)
        {
            arrived();
            return;
        }

        if (_framesSinceTarget > PathSettleFrames && agent.IsNavigationFinished())
        {
            blocked();
            return;
        }

        var heading = Flat(next - body.GlobalPosition);
        body.Velocity = heading.LengthSquared() > 0.0001f
            ? new Vector3(heading.Normalized().X, 0f, heading.Normalized().Y) * speed
            : Vector3.Zero;
        body.MoveAndSlide();
        TrackProgress(delta, stuckSeconds);
    }

    private static Vector2 Flat(Vector3 v) => new(v.X, v.Z);

    private Vector3 OnMesh(Vector3 point) => NavigationServer3D.MapGetClosestPoint(agent.GetNavigationMap(), point);

    private void TrackProgress(double delta, float stuckSeconds)
    {
        _stuckElapsed += (float)delta;
        if (Flat(body.GlobalPosition - _anchor).Length() >= ProgressMeters)
        {
            _anchor = body.GlobalPosition;
            _stuckElapsed = 0f;
        }
        else if (_stuckElapsed >= stuckSeconds)
        {
            blocked();
        }
    }
}
