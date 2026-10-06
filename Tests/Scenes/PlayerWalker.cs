using System.Threading.Tasks;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>What a walk to one waypoint did to the player: whether it arrived and how it moved vertically.</summary>
public readonly record struct WalkResult(bool Arrived, float MinY, float MaxY, int AirborneFrames, int Frames);

/// <summary>
/// Drives the production player through PlayerController's own movement loop (input action, MoveAndSlide,
/// gravity) by yawing the body towards a target and holding "ui_up".
/// </summary>
public static class PlayerWalker
{
    public const float ArrivalDistance = 0.25f;
    public const int MaxFramesPerWaypoint = 600;

    public static async Task<WalkResult> WalkTo(CharacterBody3D player, Vector2 target, int maxFrames = MaxFramesPerWaypoint)
    {
        var minY = player.GlobalPosition.Y;
        var maxY = minY;
        var airborne = 0;

        for (var frame = 0; frame < maxFrames; frame++)
        {
            var offset = target - new Vector2(player.GlobalPosition.X, player.GlobalPosition.Z);
            if (offset.Length() < ArrivalDistance)
            {
                Input.ActionRelease("ui_up");
                return new WalkResult(true, minY, maxY, airborne, frame);
            }

            // "ui_up" moves along local -Z, so yaw the body until -Z points at the waypoint.
            player.Rotation = new Vector3(0f, Mathf.Atan2(-offset.X, -offset.Y), 0f);
            Input.ActionPress("ui_up");
            await ISceneRunner.SyncPhysicsFrame;

            minY = Mathf.Min(minY, player.GlobalPosition.Y);
            maxY = Mathf.Max(maxY, player.GlobalPosition.Y);
            if (!player.IsOnFloor())
                airborne++;
        }

        Input.ActionRelease("ui_up");
        return new WalkResult(false, minY, maxY, airborne, maxFrames);
    }
}
