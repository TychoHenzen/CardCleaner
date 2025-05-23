using Godot;

public partial class PauseController : Node
{
    [Export] public CanvasLayer PauseMenuLayer;
    private Control _pauseMenuRoot;

    public override void _Ready()
    {
        // Keep this manager running always so it can un-pause
        ProcessMode = ProcessModeEnum.Always;

        // The Panel (or root Control) under your CanvasLayer
        _pauseMenuRoot = PauseMenuLayer.GetNode<Control>("Panel");
        _pauseMenuRoot.Visible = false;

        // Only process (draw & handle UI) when the tree is paused
        _pauseMenuRoot.SetProcessMode(ProcessModeEnum.WhenPaused);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!@event.IsActionPressed("ui_cancel"))
            return;

        bool nowPaused = !GetTree().Paused;
        GetTree().Paused = nowPaused;

        _pauseMenuRoot.Visible = nowPaused;

        Input.MouseMode = nowPaused
            ? Input.MouseModeEnum.Visible
            : Input.MouseModeEnum.Captured;
    }
}