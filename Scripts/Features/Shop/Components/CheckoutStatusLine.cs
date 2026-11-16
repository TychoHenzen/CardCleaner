using System;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     A checkout counter's sign: the step of the current sale, then the last message (<see cref="CheckoutCounterText" />).
///     The label is written only when its text changes, because the counter redraws it every frame.
/// </summary>
public sealed class CheckoutStatusLine
{
    private readonly Func<CheckoutSession?> _session;
    private readonly Func<bool> _claimed;
    private string _message = string.Empty;

    /// <param name="session">The counter's current sale, or null.</param>
    /// <param name="claimed">True while the cashier holds the sale.</param>
    public CheckoutStatusLine(Func<CheckoutSession?> session, Func<bool> claimed) =>
        (_session, _claimed) = (session, claimed);

    /// <summary>The label that shows the text. Nothing is shown while it is null.</summary>
    public Label3D? Label { get; set; }

    /// <summary>Shows <paramref name="message" /> under the sale's step.</summary>
    public void Say(string message)
    {
        _message = message;
        Update();
    }

    public void Update()
    {
        if (Label == null)
            return;

        var text = CheckoutCounterText.For(_session(), _claimed(), _message);
        if (Label.Text != text)
            Label.Text = text;
    }
}
