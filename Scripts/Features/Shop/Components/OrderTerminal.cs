using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Player.Controllers;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Scripts.Features.Shop.Ui;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     A PC the player looks at and clicks to open an ordering screen for <see cref="Catalog" />.
///     While the screen is open the player cannot move or look, and the mouse cursor is released;
///     closing it restores both.
/// </summary>
public partial class OrderTerminal : StaticBody3D, IInteractable
{
    private const float DefaultInteractionRange = 4.5f;
    private const uint InteractableLayerBit = 4; // Layer 3, in the InteractionSystem mask

    private Input.MouseModeEnum _mouseModeBeforeOpen = Input.MouseModeEnum.Visible;
    private PlayerController? _player;

    /// <summary>Items this terminal sells. Assign a different catalog for a different terminal.</summary>
    [Export]
    public OrderCatalog? Catalog { get; set; }

    [Export]
    public OrderTerminalUi? Ui { get; set; }

    [Export]
    public Node3D? HighlightMesh { get; set; }

    [Export]
    public float InteractionRange { get; set; } = DefaultInteractionRange;

    /// <summary>Resolved from the service locator at startup; assignable directly in tests.</summary>
    public IMoneyService? Money { get; set; }

    /// <summary>Resolved from the service locator at startup; assignable directly in tests.</summary>
    public IOrderingService? Ordering { get; set; }

    public bool IsOpen { get; private set; }

    public bool CanInteract => !IsOpen && Catalog != null && Ui != null;

    public Node3D InteractionBody => this;

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

        if (Catalog == null || Ui == null)
        {
            ILog.Error($"{Name} needs a Catalog and a Ui assigned");
            return;
        }

        Ui.Populate(Catalog);
        Ui.OrderRequested += OnOrderRequested;
        Ui.CloseRequested += Close;

        ServiceLocator.Get<IOrderingService>(ordering => Ordering = ordering);
        ServiceLocator.Get<IMoneyService>(money =>
        {
            Money = money;
            Money.BalanceChanged += Ui.SetBalance;
            Ui.SetBalance(Money.Balance);
        });
    }

    public override void _ExitTree()
    {
        if (IsOpen)
            Close();
        if (Money != null && Ui != null)
            Money.BalanceChanged -= Ui.SetBalance;
    }

    public void Open()
    {
        if (IsOpen || Ui == null)
            return;

        IsOpen = true;
        ClearHighlight();
        if (Money != null)
            Ui.SetBalance(Money.Balance);
        Ui.ShowMessage(string.Empty);
        Ui.Visible = true;

        _mouseModeBeforeOpen = Input.MouseMode;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        _player = GetTree().GetFirstNodeInGroup("player") as PlayerController;
        if (_player != null)
            _player.ControlEnabled = false;
    }

    public void Close()
    {
        if (!IsOpen)
            return;

        IsOpen = false;
        if (Ui != null)
            Ui.Visible = false;

        Input.MouseMode = _mouseModeBeforeOpen;
        if (_player != null && GodotObject.IsInstanceValid(_player))
            _player.ControlEnabled = true;
        _player = null;
    }

    private void OnOrderRequested(OrderItem item)
    {
        if (Ordering == null || Ui == null)
        {
            Ui?.ShowMessage("Ordering is unavailable right now.");
            return;
        }

        var result = Ordering.Order(item);
        Ui.ShowMessage(result.Status switch
        {
            OrderStatus.Success => $"Ordered {item.DisplayName}. It is at the delivery point.",
            OrderStatus.InsufficientFunds => $"Not enough money for {item.DisplayName}.",
            _ => $"{item.DisplayName} cannot be ordered right now."
        });
    }
}
