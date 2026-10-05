using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Ui;

/// <summary>
///     Ordering screen. Its rows are built from an <see cref="OrderCatalog" /> at runtime, so a different
///     catalog needs no UI changes. It only reports clicks; the terminal decides what an order does.
/// </summary>
public partial class OrderTerminalUi : CanvasLayer
{
    private const int PanelWidth = 420;
    private const int Margin = 16;
    private const string CloseAction = "ui_cancel";

    private readonly List<Button> _itemButtons = [];
    private Label _balanceLabel = null!;
    private VBoxContainer _itemList = null!;
    private Label _messageLabel = null!;
    private Button _closeButton = null!;
    private bool _layoutBuilt;

    /// <summary>Raised when the player clicks an item row.</summary>
    public event Action<OrderItem>? OrderRequested;

    /// <summary>Raised when the player closes the screen with the button or Escape.</summary>
    public event Action? CloseRequested;

    public int ItemRowCount => _itemButtons.Count;

    public string BalanceText
    {
        get
        {
            EnsureLayout();
            return _balanceLabel.Text;
        }
    }

    public string MessageText
    {
        get
        {
            EnsureLayout();
            return _messageLabel.Text;
        }
    }

    public override void _Ready()
    {
        Visible = false;
        EnsureLayout();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible || !@event.IsActionPressed(CloseAction))
            return;

        GetViewport().SetInputAsHandled();
        CloseRequested?.Invoke();
    }

    /// <summary>Rebuilds the item rows from <paramref name="catalog" />.</summary>
    public void Populate(OrderCatalog catalog)
    {
        EnsureLayout();
        foreach (var button in _itemButtons)
            button.QueueFree();
        _itemButtons.Clear();

        foreach (var item in catalog.Items)
        {
            var button = new Button
            {
                Text = $"{item.DisplayName}  -  {item.Price}",
                Alignment = HorizontalAlignment.Left,
                TooltipText = item.Id
            };
            button.Pressed += () => OrderRequested?.Invoke(item);
            _itemList.AddChild(button);
            _itemButtons.Add(button);
        }
    }

    public void SetBalance(int balance)
    {
        EnsureLayout();
        _balanceLabel.Text = $"Balance: {balance}";
    }

    public void ShowMessage(string message)
    {
        EnsureLayout();
        _messageLabel.Text = message;
    }

    /// <summary>Presses the n-th item row, as a click would.</summary>
    internal void PressItem(int index) => _itemButtons[index].EmitSignal(BaseButton.SignalName.Pressed);

    /// <summary>Presses the close button, as a click would.</summary>
    internal void PressClose() => _closeButton.EmitSignal(BaseButton.SignalName.Pressed);

    // The terminal can populate this screen before the screen's own _Ready has run, so every
    // entry point builds the layout on first use.
    private void EnsureLayout()
    {
        if (_layoutBuilt)
            return;
        _layoutBuilt = true;

        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(PanelWidth, 0),
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            AnchorTop = 0.5f,
            AnchorBottom = 0.5f,
            OffsetLeft = -PanelWidth / 2f,
            OffsetRight = PanelWidth / 2f,
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.Both
        };
        AddChild(panel);

        var margin = new MarginContainer();
        foreach (var side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
            margin.AddThemeConstantOverride(side, Margin);
        panel.AddChild(margin);

        var column = new VBoxContainer();
        margin.AddChild(column);

        column.AddChild(new Label { Text = "Order supplies" });
        _balanceLabel = new Label();
        column.AddChild(_balanceLabel);

        _itemList = new VBoxContainer();
        column.AddChild(_itemList);

        _messageLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        column.AddChild(_messageLabel);

        _closeButton = new Button { Text = "Close" };
        _closeButton.Pressed += () => CloseRequested?.Invoke();
        column.AddChild(_closeButton);
    }
}
