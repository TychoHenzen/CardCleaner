using System;
using System.Collections.Generic;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Checkout;

/// <summary>
///     The cash payment panel: one button per change denomination, Clear and Confirm, with a readout of the
///     amount due, the cash handed over, the change to give and the change picked so far. It reports presses
///     and holds no payment rule of its own.
/// </summary>
public partial class CheckoutChangePanel : VBoxContainer
{
    private const int Columns = 3;
    private const int ReadoutFontSize = 22;

    private readonly Dictionary<int, Button> _denominations = new();
    private Label _readout = null!;
    private Button _clear = null!;
    private Button _confirm = null!;

    /// <summary>Raised with the denomination when one of its buttons is pressed.</summary>
    public event Action<int>? DenominationPressed;

    /// <summary>Raised when Clear is pressed.</summary>
    public event Action? ClearPressed;

    /// <summary>Raised when Confirm is pressed.</summary>
    public event Action? ConfirmPressed;

    public Button ClearButton => _clear;

    public Button ConfirmButton => _confirm;

    /// <summary>The line that says what the customer owes, handed over and gets back.</summary>
    public Label Readout => _readout;

    /// <summary>The button for one change <paramref name="denomination" />.</summary>
    public Button DenominationButton(int denomination) => _denominations[denomination];

    public override void _Ready()
    {
        _readout = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _readout.AddThemeFontSizeOverride("font_size", ReadoutFontSize);
        AddChild(_readout);

        // Keys share the keypad's size and grow with their row, so the columns stay equally wide.
        var grid = new GridContainer { Columns = Columns, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        AddChild(grid);
        foreach (var denomination in PaymentRules.ChangeDenominations)
        {
            var value = denomination;
            var button = CheckoutKeypadPanel.Plain(value.ToString(), () => DenominationPressed?.Invoke(value));
            _denominations[value] = button;
            grid.AddChild(button);
        }

        var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        AddChild(actions);
        _clear = CheckoutKeypadPanel.Plain("Clear", () => ClearPressed?.Invoke());
        actions.AddChild(_clear);
        _confirm = CheckoutKeypadPanel.Plain("Confirm", () => ConfirmPressed?.Invoke());
        actions.AddChild(_confirm);
    }

    /// <summary>
    ///     Shows the <paramref name="total" /> due, the cash <paramref name="tendered" />, the change that is owed
    ///     back and the change <paramref name="picked" /> so far.
    /// </summary>
    public void ShowChange(int picked, int total, int tendered)
    {
        var owed = PaymentRules.Change(tendered, total);
        _readout.Text = $"Due {total}   Handed {tendered}\nGive back {owed}   Picked {picked}";
    }
}
