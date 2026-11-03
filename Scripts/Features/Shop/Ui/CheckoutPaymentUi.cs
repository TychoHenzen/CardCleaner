using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Pause.Models;
using CardCleaner.Scripts.Features.Player.Controllers;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Ui;

/// <summary>
///     The payment screen of the checkout counter. It opens when the counter is ready for payment and shows the
///     keypad for a card customer or the change panel for a cash customer. Each attempt goes to the counter, which
///     decides it. A refused attempt clears the entry and keeps the screen open; Escape or Cancel closes it without
///     paying.
/// </summary>
public partial class CheckoutPaymentUi : CanvasLayer, IDependent
{
    private const int MaxDigits = 6;
    private const int PanelWidth = 420;
    private const int Margin = 16;
    private const string CloseAction = "ui_cancel";
    private const string RefusedMessage = "Not accepted. Check the amount and try again.";

    private readonly List<int> _picks = new();
    private IServiceResolver? _services;
    private ICursorOwner? _cursor;
    private PlayerController? _player;
    private CheckoutSession? _session;
    private string _typed = string.Empty;
    private CheckoutKeypadPanel _keypad = null!;
    private CheckoutChangePanel _change = null!;
    private Label _message = null!;

    [Export]
    public CheckoutCounter? Counter { get; set; }

    public CheckoutKeypadPanel Keypad => _keypad;

    public CheckoutChangePanel ChangePanel => _change;

    public Label MessageText => _message;

    public string TypedText => _typed;

    public void Inject(IServiceResolver services) => _services = services;

    public override void _Ready()
    {
        Visible = false;
        BuildLayout();
        _services?.Get<ICursorOwner>(cursor => _cursor = cursor);
        if (Counter != null)
        {
            Counter.PaymentReady += Open;
            Counter.SessionEnded += Close;
        }

        _keypad.DigitPressed += AppendDigit;
        _keypad.ClearPressed += ClearTyped;
        _keypad.ConfirmPressed += PayByCard;
        _change.DenominationPressed += AddPick;
        _change.ClearPressed += ClearPicks;
        _change.ConfirmPressed += PayByCash;
    }

    public override void _ExitTree()
    {
        // Freed while open: give the cursor and the player's control back.
        Close();
        if (Counter != null)
        {
            Counter.PaymentReady -= Open;
            Counter.SessionEnded -= Close;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible || !@event.IsActionPressed(CloseAction))
            return;

        GetViewport().SetInputAsHandled();
        Close();
    }

    // Shows the screen for the front customer's method. A second request while open only refreshes the entry.
    private void Open(CheckoutSession session)
    {
        if (Counter?.Front is not { } customer)
            return;

        var wasOpen = Visible;
        _session = session;
        _typed = string.Empty;
        _picks.Clear();
        _message.Text = string.Empty;
        var card = customer.Method == PaymentMethod.Card;
        _keypad.Visible = card;
        _change.Visible = !card;

        if (!wasOpen)
        {
            Visible = true;
            _cursor?.Claim(this);
            _player = GetTree().GetFirstNodeInGroup("player") as PlayerController;
            if (_player != null)
                _player.ControlEnabled = false;
        }

        Refresh();
    }

    // Idempotent: paying, cancelling and the counter ending the session may each call it.
    private void Close()
    {
        if (!Visible)
            return;

        Visible = false;
        _session = null;
        _typed = string.Empty;
        _picks.Clear();
        _message.Text = string.Empty;
        _cursor?.Release(this);
        if (_player != null)
        {
            _player.ControlEnabled = true;
            _player = null;
        }
    }

    private void AppendDigit(int digit)
    {
        if (_typed.Length >= MaxDigits)
            return;

        _typed += digit.ToString();
        _message.Text = string.Empty;
        Refresh();
    }

    private void ClearTyped()
    {
        _typed = string.Empty;
        Refresh();
    }

    private void PayByCard()
    {
        var amount = int.TryParse(_typed, out var typed) ? typed : 0;
        if (Counter != null && Counter.PayCard(amount))
            return;

        _typed = string.Empty;
        _message.Text = RefusedMessage;
        Refresh();
    }

    private void AddPick(int denomination)
    {
        _picks.Add(denomination);
        _message.Text = string.Empty;
        Refresh();
    }

    private void ClearPicks()
    {
        _picks.Clear();
        Refresh();
    }

    // The counter receives a copy of the picks, so the screen never hands out its own list.
    private void PayByCash()
    {
        if (Counter != null && Counter.PayCash(_picks.ToArray()))
            return;

        _picks.Clear();
        _message.Text = RefusedMessage;
        Refresh();
    }

    private void Refresh()
    {
        if (_session == null)
            return;

        _keypad.ShowEntry(_typed, _session.Total);
        _change.ShowPicked(_picks.Sum());
    }

    private void BuildLayout()
    {
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
        column.AddChild(new Label { Text = "Take payment" });

        _keypad = new CheckoutKeypadPanel();
        column.AddChild(_keypad);
        _change = new CheckoutChangePanel();
        column.AddChild(_change);

        _message = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        column.AddChild(_message);

        var cancel = new Button { Text = "Cancel" };
        cancel.Pressed += Close;
        column.AddChild(cancel);
    }
}
