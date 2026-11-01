using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Features.Pause.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Pause.Components;

/// <summary>
///     Applies the mouse mode for the whole game: the cursor is visible while
///     any owner holds a claim, and captured otherwise. Place it in a scene
///     under an AutoServiceProvider.
/// </summary>
[Service(typeof(ICursorOwner))]
public partial class CursorOwner : Node, ICursorOwner
{
    public CursorClaims Claims { get; } = new();

    public Input.MouseModeEnum AppliedMode { get; private set; }

    public override void _Ready()
    {
        // Keep applying the mode while the game is paused, so a pause menu's
        // claim still shows the cursor.
        ProcessMode = ProcessModeEnum.Always;
        Apply();
    }

    public void Claim(object owner)
    {
        Claims.Claim(owner);
        Apply();
    }

    public void Release(object owner)
    {
        Claims.Release(owner);
        Apply();
    }

    public bool HasClaimOtherThan(object owner) =>
        Claims.HasClaimOtherThan(owner);

    private void Apply()
    {
        AppliedMode = Claims.HasClaims
            ? Input.MouseModeEnum.Visible
            : Input.MouseModeEnum.Captured;
        Input.MouseMode = AppliedMode;
    }
}
