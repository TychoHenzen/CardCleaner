using System;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Pause.Models;
using CardCleaner.Scripts.Features.Player.Controllers;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     A PC the player clicks to open its screen. Open freezes the player and releases the cursor; close
///     restores both. Derived terminals supply the screen in <see cref="OnOpening" /> and <see cref="OnClosing" />.
/// </summary>
public partial class ScreenTerminal : StaticBody3D, IInteractable, IDependent
{
    private const float DefaultInteractionRange = 4.5f;
    // Layer 3, in the InteractionSystem mask
    private const uint InteractableLayerBit = 4;
    private const string CloseAction = "ui_cancel";

    private PlayerController? _player;
    private ICursorOwner? _cursor;

    [Export]
    public Node3D? HighlightMesh { get; set; }

    [Export]
    public float InteractionRange { get; set; } = DefaultInteractionRange;

    /// <summary>
    ///     Raised when the terminal opens, before the cursor is claimed.
    /// </summary>
    public event Action? Opened;

    /// <summary>
    ///     Raised when the terminal closes, before the cursor is released.
    /// </summary>
    public event Action? Closed;

    public bool IsOpen { get; private set; }

    public virtual bool CanInteract => !IsOpen;

    public Node3D InteractionBody => this;

    /// <summary>
    ///     The services this terminal resolves, set by <see cref="Inject" />.
    /// </summary>
    protected IServiceResolver? Services { get; private set; }

    protected virtual bool CanOpen => true;

    public void Inject(IServiceResolver services) => Services = services;

    public void Interact()
    {
        if (CanInteract)
            Open();
    }

    public void Highlight()
    {
        if (HighlightMesh != null)
            HighlightMesh.Visible = true;
    }

    public void ClearHighlight()
    {
        if (HighlightMesh != null)
            HighlightMesh.Visible = false;
    }

    public override void _Ready()
    {
        CollisionLayer |= InteractableLayerBit;
        ClearHighlight();

        var services = Services;
        if (services == null)
        {
            ILog.Error($"{Name} was not injected");
            return;
        }

        services.Get<ICursorOwner>(cursor => _cursor = cursor);
    }

    public override void _ExitTree()
    {
        if (IsOpen)
            Close();
    }

    // Escape must close the PC even while the desktop is open: the desktop
    // sits in a SubViewport, which does not receive the key, so the terminal
    // in the main tree handles it itself.
    public override void _UnhandledInput(InputEvent @event)
    {
        if (!IsOpen || !@event.IsActionPressed(CloseAction))
            return;

        GetViewport().SetInputAsHandled();
        Close();
    }

    public void Open()
    {
        if (IsOpen || !CanOpen)
            return;

        IsOpen = true;
        ClearHighlight();
        OnOpening();
        Opened?.Invoke();

        _cursor?.Claim(this);
        _player = GetTree().GetFirstNodeInGroup("player") as PlayerController;
        if (_player != null)
            _player.ControlEnabled = false;
    }

    public void Close()
    {
        if (!IsOpen)
            return;

        IsOpen = false;
        OnClosing();
        Closed?.Invoke();

        _cursor?.Release(this);
        if (_player != null && GodotObject.IsInstanceValid(_player))
            _player.ControlEnabled = true;
        _player = null;
    }

    /// <summary>
    ///     Shows the screen. Runs while the terminal is open, before
    ///     <see cref="Opened" />.
    /// </summary>
    protected virtual void OnOpening()
    {
    }

    /// <summary>
    ///     Hides the screen. Runs after the terminal is marked closed, before
    ///     <see cref="Closed" />.
    /// </summary>
    protected virtual void OnClosing()
    {
    }
}
