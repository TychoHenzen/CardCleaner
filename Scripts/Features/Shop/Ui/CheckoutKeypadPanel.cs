using System;
using System.Collections.Generic;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Ui;

/// <summary>
///     The card payment keypad: digits 0 to 9, Clear and Confirm, with a readout of the typed total and the
///     total due. It reports presses and holds no payment rule of its own.
/// </summary>
public partial class CheckoutKeypadPanel : VBoxContainer
{
    private const int LastDigit = 9;
    private const int Columns = 3;

    private readonly Dictionary<int, Button> _digits = new();
    private Label _readout = null!;
    private Button _clear = null!;
    private Button _confirm = null!;

    /// <summary>Raised with the digit when a digit button is pressed.</summary>
    public event Action<int>? DigitPressed;

    /// <summary>Raised when Clear is pressed.</summary>
    public event Action? ClearPressed;

    /// <summary>Raised when Confirm is pressed.</summary>
    public event Action? ConfirmPressed;

    public Button ClearButton => _clear;

    public Button ConfirmButton => _confirm;

    /// <summary>The button for <paramref name="digit" />, 0 to 9.</summary>
    public Button DigitButton(int digit) => _digits[digit];

    public override void _Ready()
    {
        _readout = new Label();
        AddChild(_readout);

        var grid = new GridContainer { Columns = Columns };
        AddChild(grid);
        for (var digit = 1; digit <= LastDigit; digit++)
            grid.AddChild(Digit(digit));

        _clear = Plain("Clear", () => ClearPressed?.Invoke());
        grid.AddChild(_clear);
        grid.AddChild(Digit(0));
        _confirm = Plain("Confirm", () => ConfirmPressed?.Invoke());
        grid.AddChild(_confirm);
    }

    /// <summary>Shows the digits typed so far and the total they are paid against.</summary>
    public void ShowEntry(string typed, int total)
    {
        _readout.Text = $"Typed: {typed}   Total: {total}";
    }

    private Button Digit(int digit)
    {
        var button = Plain(digit.ToString(), () => DigitPressed?.Invoke(digit));
        _digits[digit] = button;
        return button;
    }

    private static Button Plain(string text, Action onPressed)
    {
        var button = new Button { Text = text };
        button.Pressed += onPressed;
        return button;
    }
}
