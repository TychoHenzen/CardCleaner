using System;
using Godot;

namespace CardCleaner.Scripts.Core.Data;

public class InputAction
{
    public required string Name { get; init; }
    public Key? Key { get; set; }
    public MouseButton? MouseButton { get; set; }
    public required object Owner { get; init; }
    public Action? Callback { get; init; }
    public Action<bool>? MouseCallback { get; init; } // For mouse actions that need press/release

    public bool Matches(Key key)
    {
        return Key == key;
    }

    public bool Matches(MouseButton button)
    {
        return MouseButton == button;
    }
}