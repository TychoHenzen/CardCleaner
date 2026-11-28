namespace CardCleaner.Scripts.Features.Shop.Checkout;

/// <summary>
///     What the register's display says, as a till shows it: while scanning, how many items are scanned and their
///     running subtotal; once all are scanned, how the customer pays and the total (and, for cash, what they handed
///     over); then the register's last message on its own line. With no sale and no message it is blank. The shop's
///     money is on the HUD, so the register does not repeat it.
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
            $"Items {scanning.ScannedCount} of {scanning.ItemCount}\nSubtotal {scanning.ScannedTotal}",
        { State: CheckoutState.AwaitingPayment, Method: PaymentMethod.Card } card =>
            $"Card payment\nTotal {card.Total}",
        { State: CheckoutState.AwaitingPayment } cash => $"Cash payment\nTotal {cash.Total}, given {cash.Tendered}",
        _ => string.Empty,
    };
}
