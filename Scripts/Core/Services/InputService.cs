using System;
using System.Collections.Generic;
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
    private readonly Dictionary<Key, List<InputAction>> _keyActions = new();
    private readonly Dictionary<MouseButton, List<InputAction>> _mouseActions = new();

    // Raw input events
    public event Action<Vector2>? MouseMoved;
    public event Action<MouseButton, bool>? MouseButtonChanged;
    public event Action<Key, bool>? KeyChanged;

    // Movement input (polled)
    public Vector2 MovementInput { get; private set; }

    public void RegisterAction(object owner, string actionName, Key key, Action callback)
    {
        var action = new InputAction
        {
            Name = actionName,
            Key = key,
            Owner = owner,
            Callback = callback
        };

        _registeredActions.Add(action);
        _actionStates[actionName] = false;

        if (!_keyActions.TryGetValue(key, out var actions))
            _keyActions[key] = actions = new List<InputAction>();
        actions.Add(action);

        ILog.Print($"Registered action '{actionName}' -> {key}");
    }

    public void RegisterAction(object owner, string actionName, MouseButton button, Action<bool> callback)
    {
        var action = new InputAction
        {
            Name = actionName,
            MouseButton = button,
            Owner = owner,
            MouseCallback = callback
        };

        _registeredActions.Add(action);
        _actionStates[actionName] = false;

        if (!_mouseActions.TryGetValue(button, out var actions))
            _mouseActions[button] = actions = new List<InputAction>();
        actions.Add(action);

        ILog.Print($"Registered action '{actionName}' -> {button}");
    }

    public void UnregisterAction(object owner, string actionName)
    {
        var removed = _registeredActions.Where(a => a.Name == actionName && a.Owner.Equals(owner)).ToList();
        foreach (var action in removed)
        {
            _registeredActions.Remove(action);

            if (action.Key.HasValue && _keyActions.TryGetValue(action.Key.Value, out var keyList))
                keyList.Remove(action);

            if (action.MouseButton.HasValue && _mouseActions.TryGetValue(action.MouseButton.Value, out var mouseList))
                mouseList.Remove(action);
        }

        _actionStates.Remove(actionName);
    }

    public void UnregisterAllActions(object owner)
    {
        var actionsToRemove = _registeredActions.Where(a => a.Owner.Equals(owner)).ToList();
        foreach (var action in actionsToRemove)
        {
            _registeredActions.Remove(action);

            if (action.Key.HasValue && _keyActions.TryGetValue(action.Key.Value, out var keyList))
                keyList.Remove(action);

            if (action.MouseButton.HasValue && _mouseActions.TryGetValue(action.MouseButton.Value, out var mouseList))
                mouseList.Remove(action);

            _actionStates.Remove(action.Name);
        }
    }

    public void RemapAction(string actionName, Key newKey)
    {
        var action = _registeredActions.FirstOrDefault(a => a.Name == actionName);
        if (action == null)
            return;

        // Remove from old dictionary
        if (action.Key.HasValue && _keyActions.TryGetValue(action.Key.Value, out var oldKeyList))
            oldKeyList.Remove(action);

        if (action.MouseButton.HasValue && _mouseActions.TryGetValue(action.MouseButton.Value, out var oldMouseList))
            oldMouseList.Remove(action);

        // Update action
        action.Key = newKey;
        action.MouseButton = null;

        // Add to new dictionary
        if (!_keyActions.TryGetValue(newKey, out var newKeyList))
            _keyActions[newKey] = newKeyList = new List<InputAction>();
        newKeyList.Add(action);

        ILog.Print($"Remapped '{actionName}' to {newKey}");
    }

    public void RemapAction(string actionName, MouseButton newButton)
    {
        var action = _registeredActions.FirstOrDefault(a => a.Name == actionName);
        if (action == null)
            return;

        // Remove from old dictionary
        if (action.Key.HasValue && _keyActions.TryGetValue(action.Key.Value, out var oldKeyList))
            oldKeyList.Remove(action);

        if (action.MouseButton.HasValue && _mouseActions.TryGetValue(action.MouseButton.Value, out var oldMouseList))
            oldMouseList.Remove(action);

        // Update action
        action.MouseButton = newButton;
        action.Key = null;

        // Add to new dictionary
        if (!_mouseActions.TryGetValue(newButton, out var newMouseList))
            _mouseActions[newButton] = newMouseList = new List<InputAction>();
        newMouseList.Add(action);

        ILog.Print($"Remapped '{actionName}' to {newButton}");
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

        // Check registered actions - O(1) dictionary lookup instead of LINQ
        if (_mouseActions.TryGetValue(mouse.ButtonIndex, out var actions))
        {
            foreach (var action in actions)
            {
                UpdateActionState(action.Name, mouse.Pressed);
                action.MouseCallback?.Invoke(mouse.Pressed);
            }
        }
    }

    private void HandleKeyboard(InputEventKey key)
    {
        KeyChanged?.Invoke(key.Keycode, key.Pressed);

        // Check registered actions - O(1) dictionary lookup instead of LINQ
        if (_keyActions.TryGetValue(key.Keycode, out var actions))
        {
            foreach (var action in actions)
            {
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

    private void RegisterDefaultActions()
    {
        // These are common actions that many components might need
        // Components can still override these by registering their own version
    }
}