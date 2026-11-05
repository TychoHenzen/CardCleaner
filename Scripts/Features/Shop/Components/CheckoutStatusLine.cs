using CardCleaner.Scripts.Features.Shop.Services;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     A checkout counter's status label: the balance of the money service it is linked to, then the last message.
///     It listens to the balance only while linked, and relinks when the counter re-enters the tree.
/// </summary>
public sealed class CheckoutStatusLine
{
    private IMoneyService? _money;
    private string _message = string.Empty;

    /// <summary>The label that shows the text. Nothing is shown while it is null.</summary>
    public Label3D? Label { get; set; }

    /// <summary>The money service whose balance is shown.</summary>
    public IMoneyService? Money => _money;

    /// <summary>Switches to <paramref name="money" />, drops the old listener and redraws.</summary>
    public void Link(IMoneyService? money)
    {
        Unlink();
        _money = money;
        Relink();
    }

    /// <summary>Listens to the current money service again and redraws.</summary>
    public void Relink()
    {
        Unlink();
        if (_money != null)
            _money.BalanceChanged += OnBalanceChanged;
        Update();
    }

    /// <summary>Stops listening to the balance.</summary>
    public void Unlink()
    {
        if (_money != null)
            _money.BalanceChanged -= OnBalanceChanged;
    }

    /// <summary>Shows <paramref name="message" /> under the balance.</summary>
    public void Say(string message)
    {
        _message = message;
        Update();
    }

    public void Update()
    {
        if (Label != null)
            Label.Text = CheckoutCounterLayout.StatusText(_money, _message);
    }

    private void OnBalanceChanged(int balance) => Update();
}
