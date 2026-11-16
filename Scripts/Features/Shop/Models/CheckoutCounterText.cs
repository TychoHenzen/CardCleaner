namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>
///     What the checkout counter's sign says: the step of the sale in front of it (scan progress, then the total to
///     take), then the counter's last message on a second line. With no sale and no message it says nothing. The
///     shop's money is on the HUD, so the counter does not repeat it.
/// </summary>
public static class CheckoutCounterText
{
    public const string CashierServing = "Cashier serving";

    public static string For(CheckoutSession? session, bool claimed, string message)
    {
        var step = StepOf(session, claimed);
        if (message.Length == 0)
            return step;

        return step.Length == 0 ? message : $"{step}\n{message}";
    }

    private static string StepOf(CheckoutSession? session, bool claimed) => session switch
    {
        null => string.Empty,
        _ when claimed => CashierServing,
        { State: CheckoutState.Scanning } scanning =>
            $"Scan the items: {scanning.ScannedCount} of {scanning.ItemCount}",
        { State: CheckoutState.AwaitingPayment, Method: PaymentMethod.Card } card => $"Card payment: {card.Total}",
        { State: CheckoutState.AwaitingPayment } cash => $"Cash payment: {cash.Total}, handed {cash.Tendered}",
        _ => string.Empty,
    };
}
