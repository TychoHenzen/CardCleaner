using System;
using Godot;

namespace CardCleaner.Scripts.Core.Interfaces
{
    public interface IInputService
    {
        // Raw input events (always available)
        event Action<Vector2> MouseMoved;
        event Action<MouseButton, bool> MouseButtonChanged; // button, pressed
        event Action<Key, bool> KeyChanged; // key, pressed
        
        // Continuous input (polled)
        Vector2 MovementInput { get; }
        bool IsActionPressed(string actionName);
        bool IsActionJustPressed(string actionName);
        bool IsActionJustReleased(string actionName);
        
        // Action registration system
        void RegisterAction(string actionName, Key key, Action callback);
        void RegisterAction(string actionName, MouseButton button, Action<bool> callback); // bool = pressed
        void UnregisterAction(string actionName, object owner);
        void UnregisterAllActions(object owner);
        
        // Key mapping management
        void RemapAction(string actionName, Key newKey);
        void RemapAction(string actionName, MouseButton newButton);
        Key GetKeyForAction(string actionName);
        MouseButton? GetMouseButtonForAction(string actionName);
    }
}