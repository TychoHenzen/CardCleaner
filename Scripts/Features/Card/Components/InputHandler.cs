using Godot;

namespace CardCleaner.Scripts.Features.Card.Components;

public partial class InputHandler : Node3D
{
    [Signal] public delegate void RightPressEventHandler();
    [Signal] public delegate void RightReleaseEventHandler();
    [Signal] public delegate void LeftPressEventHandler();
    [Signal] public delegate void KeyPressedEventHandler(InputEventKey keycode);

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb)
        {
            if (mb.ButtonIndex == MouseButton.Right)
            {
                if (mb.Pressed) EmitSignal(nameof(RightPress));
                else EmitSignal(nameof(RightRelease));
            }
            else if (mb.ButtonIndex == MouseButton.Left && mb.Pressed)
            {
                EmitSignal(nameof(LeftPress));
            }
        }
        if (@event is InputEventKey { Pressed: true, Echo: false } keyEvt)
        {
            EmitSignal(nameof(KeyPressed), keyEvt);
        }
    }
}