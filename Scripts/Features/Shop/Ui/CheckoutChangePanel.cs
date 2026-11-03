using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Ui;

/// <summary>
///     The cash payment panel: one button per change denomination, Clear and Confirm, with a readout of the
///     change picked so far. It reports presses and holds no payment rule of its own.
/// </summary>
public partial class CheckoutChangePanel : VBoxContainer
{
    private const int Columns = 3;

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

    /// <summary>The button for one change <paramref name="denomination" />.</summary>
    public Button DenominationButton(int denomination) => _denominations[denomination];

    public override void _Ready()
    {
        _readout = new Label();
        AddChild(_readout);

        var grid = new GridContainer { Columns = Columns };
        AddChild(grid);
        foreach (var denomination in PaymentRules.ChangeDenominations)
        {
            var value = denomination;
            var button = new Button { Text = value.ToString() };
            button.Pressed += () => DenominationPressed?.Invoke(value);
            _denominations[value] = button;
            grid.AddChild(button);
        }

        _clear = new Button { Text = "Clear" };
        _clear.Pressed += () => ClearPressed?.Invoke();
        grid.AddChild(_clear);
        _confirm = new Button { Text = "Confirm" };
        _confirm.Pressed += () => ConfirmPressed?.Invoke();
        grid.AddChild(_confirm);
    }

    /// <summary>Shows the total of the change picked so far.</summary>
    public void ShowPicked(int picked)
    {
        _readout.Text = $"Change picked: {picked}";
    }
}
