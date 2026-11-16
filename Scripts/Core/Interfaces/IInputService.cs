using System;
using Godot;

namespace CardCleaner.Scripts.Core.Interfaces;

public interface IInputService
{
    // Continuous input (polled)
    Vector2 MovementInput { get; }

    // Raw input events (always available)
    event Action<Vector2> MouseMoved;
    event Action<MouseButton, bool> MouseButtonChanged; // button, pressed
    event Action<Key, bool> KeyChanged; // key, pressed
    bool IsActionPressed(string actionName);
    bool IsActionJustPressed(string actionName);
    bool IsActionJustReleased(string actionName);

    /// <summary>
    ///     While true, a press fires no registered action: a menu or screen holds the cursor, so world keys and clicks
    ///     must not act behind it. Releases still reach mouse actions, so a press made before ends cleanly. Raw events
    ///     and the polled movement are not affected.
    /// </summary>
    bool ActionsSuspended { get; set; }

    // Action registration system
    void RegisterAction(object owner, string actionName, Key key, Action callback);
    void RegisterAction(object owner, string actionName, MouseButton button, Action<bool> callback); // bool = pressed
    void UnregisterAction(object owner, string actionName);
    void UnregisterAllActions(object owner);

    // Key mapping management
    void RemapAction(string actionName, Key newKey);
    void RemapAction(string actionName, MouseButton newButton);
    Key GetKeyForAction(string actionName);
    MouseButton? GetMouseButtonForAction(string actionName);
}