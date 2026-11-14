using System.Threading.Tasks;
using Godot;

namespace CardCleaner.Scripts.Features.Automation;

/// <summary>
///     Turns the active camera to a yaw and pitch by sending relative mouse motion, the way a player would, and
///     measuring what each motion did. The degrees-per-pixel gain is learned from the camera's response, so mouse
///     sensitivity and inverted Y need no special case.
/// </summary>
public sealed class ViewAim
{
    private const float GuessGain = -0.1f;
    private const float MaxStepPixels = 400f;
    private const float MeasurableDegrees = 0.01f;
    private const int MaxSteps = 25;

    private Vector2 _gain = new(GuessGain, GuessGain);

    /// <summary>Degrees of yaw (X) and pitch (Y) per pixel of motion, as last measured.</summary>
    public Vector2 Gain => _gain;

    /// <summary>The motion that should turn the view by <paramref name="degrees" />, capped to one step.</summary>
    public Vector2 PixelsFor(Vector2 degrees) => new(
        Mathf.Clamp(degrees.X / _gain.X, -MaxStepPixels, MaxStepPixels),
        Mathf.Clamp(degrees.Y / _gain.Y, -MaxStepPixels, MaxStepPixels));

    /// <summary>Learns the gain from one motion and the turn it caused; ignores motions too small to measure.</summary>
    public void Learn(Vector2 pixels, Vector2 turned)
    {
        if (Mathf.Abs(pixels.X) >= 1f && Mathf.Abs(turned.X) > MeasurableDegrees)
            _gain.X = turned.X / pixels.X;
        if (Mathf.Abs(pixels.Y) >= 1f && Mathf.Abs(turned.Y) > MeasurableDegrees)
            _gain.Y = turned.Y / pixels.Y;
    }

    /// <summary>
    ///     Aims at <paramref name="wanted" /> (yaw, pitch in degrees; a null pitch keeps the current one). Returns the
    ///     angles reached and whether pitch hit the game's limit before reaching the target.
    /// </summary>
    public async Task<(Vector2 Reached, bool PitchLimited)> Aim(AutomationCommands commands, float wantedYaw,
        float? wantedPitch, float tolerance)
    {
        var pitchLimited = false;
        for (var step = 0; step < MaxSteps; step++)
        {
            var now = Angles(commands);
            var error = new Vector2(AutomationView.AngleDelta(now.X, wantedYaw),
                pitchLimited || wantedPitch == null ? 0f : wantedPitch.Value - now.Y);
            if (Mathf.Abs(error.X) <= tolerance && Mathf.Abs(error.Y) <= tolerance)
                return (now, pitchLimited);
            var pixels = PixelsFor(error);
            commands.Mouse.Relative(pixels, commands);
            await commands.Frames(2);
            var turned = Turned(now, Angles(commands));
            if (step == 0 && turned.Length() < MeasurableDegrees)
                throw new AutomationException("the view did not move: player control is off or a UI owns the mouse");
            pitchLimited |= Mathf.Abs(pixels.Y) >= 1f && Mathf.Abs(turned.Y) < MeasurableDegrees;
            Learn(pixels, turned);
        }

        return (Angles(commands), pitchLimited);
    }

    public static Vector2 Angles(AutomationCommands commands) =>
        AutomationView.YawPitch(AutomationView.Forward(AutomationView.RequireCamera(commands.Tree)));

    private static Vector2 Turned(Vector2 before, Vector2 after) =>
        new(AutomationView.AngleDelta(before.X, after.X), after.Y - before.Y);
}
