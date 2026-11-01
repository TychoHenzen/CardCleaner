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

        if (GetTree().Paused)
        {
            Resume();
            return;
        }

        // While something else holds the cursor (a PC screen), Escape belongs
        // to that screen, not to pause
        if (_cursor != null && _cursor.HasClaimOtherThan(this))
            return;

        Pause();
    }

    private void Pause()
    {
        GetTree().Paused = true;
        _pauseMenuRoot.Visible = true;
        if (UILayer != null)
            UILayer.Visible = false;

        // The pause menu holds a cursor claim while it is open
        _cursor?.Claim(this);
    }

    private void Resume()
    {
        GetTree().Paused = false;
        _pauseMenuRoot.Visible = false;
        if (UILayer != null)
            UILayer.Visible = true;

        _cursor?.Release(this);
    }
}
