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
    ///     While true (a menu or screen holds the cursor), a press fires no registered action, so nothing acts behind
    ///     it. Releases still reach mouse actions, so an earlier press ends cleanly; raw events and movement do not.
    /// </summary>
    bool ActionsSuspended { get; set; }

    // Action registration system
    void RegisterAction(object owner, string actionName, Key key, Action callback);
    /// <summary>
    ///     Registers a mouse action. The callback runs on every press and release of the button, the wheel included
    ///     (Godot sends a wheel step as a press and then a release).
    /// </summary>
    void RegisterAction(object owner, string actionName, MouseButton button, Action<MouseActionArgs> callback);
    void UnregisterAction(object owner, string actionName);
    void UnregisterAllActions(object owner);

    // Key mapping management
    void RemapAction(string actionName, Key newKey);
    void RemapAction(string actionName, MouseButton newButton);
    Key GetKeyForAction(string actionName);
    MouseButton? GetMouseButtonForAction(string actionName);
}