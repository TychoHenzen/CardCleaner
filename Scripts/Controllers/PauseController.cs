using Godot;

public partial class PauseController : Node
{
    [Export] public CanvasLayer PauseMenuLayer;
    [Export] public CanvasLayer UILayer;
    private Control _pauseMenuRoot;

    public override void _Ready()
    {
        // Always run so we can unpause via input
        ProcessMode = ProcessModeEnum.Always;

        // Cache and hide the pause menu
        _pauseMenuRoot = PauseMenuLayer.GetNode<Control>("Panel");
        _pauseMenuRoot.Visible = false;
        _pauseMenuRoot.SetProcessMode(ProcessModeEnum.WhenPaused);

        // Start with cursor captured and in-game UI visible
        Input.SetMouseMode(Input.MouseModeEnum.Captured);
        UILayer.Visible = true;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!@event.IsActionPressed("ui_cancel"))
            return;

        bool nowPaused = !GetTree().Paused;
        GetTree().Paused = nowPaused;

        // Show or hide pause menu
        _pauseMenuRoot.Visible = nowPaused;

        // Toggle in-game UI opposite of pause menu
        UILayer.Visible = !nowPaused;

        // Toggle cursor between captured and visible
        Input.SetMouseMode(
            nowPaused
                ? Input.MouseModeEnum.Visible
                : Input.MouseModeEnum.Captured
        );
    }
}