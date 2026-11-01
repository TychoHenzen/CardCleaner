using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Pause.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Pause.Controllers;

public partial class PauseController : Node, IDependent
{
    private Control _pauseMenuRoot = null!;
    private ICursorOwner? _cursor;
    private IServiceResolver? _services;

    [Export] public CanvasLayer PauseMenuLayer = null!;
    [Export] public CanvasLayer? UILayer;

    public void Inject(IServiceResolver services) => _services = services;

    public override void _Ready()
    {
        // Always run so we can unpause via input
        ProcessMode = ProcessModeEnum.Always;

        // Cache and hide the pause menu
        _pauseMenuRoot = PauseMenuLayer.GetNode<Control>("Panel");
        _pauseMenuRoot.Visible = false;
        _pauseMenuRoot.SetProcessMode(ProcessModeEnum.WhenPaused);
        PauseMenuLayer.GetNode<Button>("Panel/ResumeButton").Pressed += Resume;

        // In-game UI starts visible
        if (UILayer != null)
            UILayer.Visible = true;

        if (_services == null)
        {
            ILog.Error("PauseController was not injected");
            return;
        }

        _services.Get<ICursorOwner>(cursor => _cursor = cursor);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!@event.IsActionPressed("ui_cancel"))
            return;

        var paused = GetTree().Paused;

        // While something else holds the cursor (a PC screen), Escape belongs to that screen, not to pause
        if (!paused && _cursor != null && _cursor.HasClaimOtherThan(this))
            return;

        SetPaused(!paused);
    }

    private void Resume() => SetPaused(false);

    private void SetPaused(bool paused)
    {
        GetTree().Paused = paused;

        // Show or hide pause menu
        _pauseMenuRoot.Visible = paused;

        // Toggle in-game UI opposite of pause menu
        if (UILayer != null)
            UILayer.Visible = !paused;

        // The pause menu holds a cursor claim while it is open
        if (paused)
            _cursor?.Claim(this);
        else
            _cursor?.Release(this);
    }
}
