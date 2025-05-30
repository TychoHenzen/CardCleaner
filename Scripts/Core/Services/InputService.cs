using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Core.Services;

/// <summary>
///     Flexible input service supporting action registration and key remapping.
///     Components can register their own input actions without modifying this service.
/// </summary>
public partial class InputService : Node, IInputService
{
    private readonly Dictionary<string, bool> _actionJustPressed = new();
    private readonly Dictionary<string, bool> _actionJustReleased = new();
    private readonly Dictionary<string, bool> _actionStates = new();

    // Registered actions
    private readonly List<InputAction> _registeredActions = new();

    // Raw input events
    public event Action<Vector2> MouseMoved;
    public event Action<MouseButton, bool> MouseButtonChanged;
    public event Action<Key, bool> KeyChanged;

    // Movement input (polled)
    public Vector2 MovementInput { get; private set; }

    public void RegisterAction(string actionName, Key key, Action callback)
    {
        var action = new InputAction
        {
            Name = actionName,
            Key = key,
            Owner = GetStackOwner(),
            Callback = callback
        };

        _registeredActions.Add(action);
        _actionStates[actionName] = false;

        GD.Print($"[InputService] Registered action '{actionName}' -> {key}");
    }

    public void RegisterAction(string actionName, MouseButton button, Action<bool> callback)
    {
        var action = new InputAction
        {
            Name = actionName,
            MouseButton = button,
            Owner = GetStackOwner(),
            MouseCallback = callback
        };

        _registeredActions.Add(action);
        _actionStates[actionName] = false;

        GD.Print($"[InputService] Registered action '{actionName}' -> {button}");
    }

    public void UnregisterAction(string actionName, object owner)
    {
        _registeredActions.RemoveAll(a => a.Name == actionName && a.Owner == owner);
        _actionStates.Remove(actionName);
    }

    public void UnregisterAllActions(object owner)
    {
        var actionsToRemove = _registeredActions.Where(a => a.Owner == owner).ToList();
        foreach (var action in actionsToRemove)
        {
            _registeredActions.Remove(action);
            _actionStates.Remove(action.Name);
        }
    }

    public void RemapAction(string actionName, Key newKey)
    {
        var action = _registeredActions.FirstOrDefault(a => a.Name == actionName);
        if (action != null)
        {
            action.Key = newKey;
            action.MouseButton = null; // Clear mouse button if it was set
            GD.Print($"[InputService] Remapped '{actionName}' to {newKey}");
        }
    }

    public void RemapAction(string actionName, MouseButton newButton)
    {
        var action = _registeredActions.FirstOrDefault(a => a.Name == actionName);
        if (action != null)
        {
            action.MouseButton = newButton;
            action.Key = null; // Clear key if it was set
            GD.Print($"[InputService] Remapped '{actionName}' to {newButton}");
        }
    }

    public Key GetKeyForAction(string actionName)
    {
        return _registeredActions.FirstOrDefault(a => a.Name == actionName)?.Key ?? Key.Unknown;
    }

    public MouseButton? GetMouseButtonForAction(string actionName)
    {
        return _registeredActions.FirstOrDefault(a => a.Name == actionName)?.MouseButton;
    }

    public bool IsActionPressed(string actionName)
    {
        return _actionStates.GetValueOrDefault(actionName, false);
    }

    public bool IsActionJustPressed(string actionName)
    {
        return _actionJustPressed.GetValueOrDefault(actionName, false);
    }

    public bool IsActionJustReleased(string actionName)
    {
        return _actionJustReleased.GetValueOrDefault(actionName, false);
    }

    public override void _Ready()
    {
        SetProcessInput(true);

        // Register some default actions that most games need
        RegisterDefaultActions();
    }

    public override void _Input(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseMotion motion:
                MouseMoved?.Invoke(motion.Relative);
                break;

            case InputEventMouseButton mouse:
                HandleMouseButton(mouse);
                break;

            case InputEventKey { Echo: false } key:
                HandleKeyboard(key);
                break;
        }
    }

    public override void _Process(double delta)
    {
        // Update continuous input
        MovementInput = new Vector2(
            Input.GetActionStrength("ui_right") - Input.GetActionStrength("ui_left"),
            Input.GetActionStrength("ui_down") - Input.GetActionStrength("ui_up")
        );

        // Clear just-pressed/released flags
        _actionJustPressed.Clear();
        _actionJustReleased.Clear();
    }

    private void HandleMouseButton(InputEventMouseButton mouse)
    {
        MouseButtonChanged?.Invoke(mouse.ButtonIndex, mouse.Pressed);

        // Check registered actions
        var matchingActions = _registeredActions.Where(a => a.Matches(mouse.ButtonIndex));
        foreach (var action in matchingActions)
        {
            UpdateActionState(action.Name, mouse.Pressed);
            action.MouseCallback?.Invoke(mouse.Pressed);
        }
    }

    private void HandleKeyboard(InputEventKey key)
    {
        KeyChanged?.Invoke(key.Keycode, key.Pressed);

        // Check registered actions
        var matchingActions = _registeredActions.Where(a => a.Matches(key.Keycode));
        foreach (var action in matchingActions)
            if (key.Pressed)
            {
                UpdateActionState(action.Name, true);
                action.Callback?.Invoke();
            }
            else
            {
                UpdateActionState(action.Name, false);
            }
    }

    private void UpdateActionState(string actionName, bool pressed)
    {
        var wasPressed = _actionStates.GetValueOrDefault(actionName, false);
        _actionStates[actionName] = pressed;

        if (pressed && !wasPressed)
            _actionJustPressed[actionName] = true;
        else if (!pressed && wasPressed)
            _actionJustReleased[actionName] = true;
    }

    private object GetStackOwner()
    {
        // Try to get the calling object from the stack
        var frame = new StackFrame(2);
        var method = frame.GetMethod();
        return method?.DeclaringType?.Name ?? "Unknown";
    }

    private void RegisterDefaultActions()
    {
        // These are common actions that many components might need
        // Components can still override these by registering their own version
    }
}