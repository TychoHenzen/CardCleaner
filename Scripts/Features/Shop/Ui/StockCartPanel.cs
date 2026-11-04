using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Ui;

/// <summary>
///     The cart half of the Stock app: the cart rows, the total, the message line and the Checkout button. It
///     shows an <see cref="OrderCart" /> and reports presses back to the app; the rules stay in the cart.
/// </summary>
internal sealed class StockCartPanel
{
    private readonly OrderCart _cart;
    private readonly Func<int> _level;
    private readonly Dictionary<string, Button> _minusButtons = new();
    private readonly Action _onCheckout;

    // The controls are made in AddTo, not here: a StockApp that is instanced but never enters the tree would
    // otherwise keep these parentless nodes as orphans.
    private VBoxContainer _rows = null!;
    private Label _total = null!;
    private Label _message = null!;
    private Button _checkout = null!;

    public StockCartPanel(OrderCart cart, Func<int> level, Action onCheckout)
    {
        _cart = cart;
        _level = level;
        _onCheckout = onCheckout;
    }

    internal int LineCount => _cart.Lines.Count;

    internal string TotalText => _total.Text;

    internal bool CheckoutDisabled => _checkout.Disabled;

    internal string MessageText => _message.Text;

    /// <summary>Adds the panel's controls to <paramref name="column" />.</summary>
    public void AddTo(VBoxContainer column)
    {
        _rows = new VBoxContainer();
        _total = new Label { Text = "Total: 0" };
        _message = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _checkout = new Button { Text = "Checkout", Disabled = true };
        _checkout.Pressed += _onCheckout;
        column.AddChild(_rows);
        column.AddChild(_total);
        column.AddChild(_message);
        column.AddChild(_checkout);
    }

    public void ShowMessage(string message) => _message.Text = message;

    /// <summary>Rebuilds the rows, the total and the Checkout button from the cart.</summary>
    public void Refresh()
    {
        // A row's own button raises the signal that rebuilds the rows, so the old rows cannot be freed at once.
        // They are hidden and queued while still in the tree: never detached, so never orphans.
        foreach (var child in _rows.GetChildren())
        {
            ((Control)child).Hide();
            child.QueueFree();
        }
        _minusButtons.Clear();

        foreach (var line in _cart.Lines)
            _rows.AddChild(BuildRow(line));
        _total.Text = $"Total: {_cart.Total}";
        _checkout.Disabled = _cart.IsEmpty;
    }

    /// <summary>Presses the minus button of the cart line for the item, as a click would.</summary>
    public void PressRemoveOne(string id) => _minusButtons[id].EmitSignal(BaseButton.SignalName.Pressed);

    private HBoxContainer BuildRow(CartLine line)
    {
        var id = line.Entry.Id;
        var row = new HBoxContainer();
        row.AddChild(new Label
        {
            Text = $"{line.Entry.Name} x{line.Quantity}",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        });

        var minus = RowButton("-", () => _cart.RemoveOne(id));
        _minusButtons[id] = minus;
        row.AddChild(minus);
        row.AddChild(RowButton("+", () => _cart.TryAdd(line.Entry, _level())));
        row.AddChild(RowButton("Remove", () => _cart.RemoveLine(id)));
        return row;
    }

    private Button RowButton(string text, Action change)
    {
        var button = new Button { Text = text };
        button.Pressed += () =>
        {
            change();
            Refresh();
        };
        return button;
    }
}
