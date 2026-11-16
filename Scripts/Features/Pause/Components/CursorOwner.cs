using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Automation;
using CardCleaner.Scripts.Features.Pause.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Pause.Components;

/// <summary>
///     Applies the mouse mode for the whole game: the cursor is visible while
///     any owner holds a claim, and captured otherwise. While a claim is held it
///     also suspends the world's key and click actions (<see cref="IInputService.ActionsSuspended" />), so a menu
///     or screen in front of the game never acts on the world behind it. Place it in a scene
///     under an AutoServiceProvider.
/// </summary>
[Service(typeof(ICursorOwner))]
public partial class CursorOwner : Node, ICursorOwner, IDependent
{
    private IServiceResolver? _services;
    private IInputService? _input;

    public CursorClaims Claims { get; } = new();

    public Input.MouseModeEnum AppliedMode { get; private set; }

    public void Inject(IServiceResolver services) => _services = services;

    public override void _Ready()
    {
        // Keep applying the mode while the game is paused, so a pause menu's
        // claim still shows the cursor.
        this.ProcessMode = ProcessModeEnum.Always;
        Apply();
        _services?.Get<IInputService>(input =>
        {
            _input = input;
            Apply();
        });
    }

    public override void _ExitTree()
    {
        // The input service outlives the scene, so the next scene must not start with the world suspended.
        if (_input != null)
            _input.ActionsSuspended = false;
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
        Input.MouseMode = AutomationLaunch.SafeMouseMode(AppliedMode);
        if (_input != null)
            _input.ActionsSuspended = Claims.HasClaims;
    }
}
