namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>
///     The guards of a payment, as plain rules: a usable register, a sale with a customer, every item scanned.
/// </summary>
public static class CheckoutPaymentGuard
{
    public const string UnavailableMessage = "The register is unavailable right now.";
    public const string ScanFirstMessage = "Scan every item first";

    /// <summary>
    ///     Judges the counter. <paramref name="sessionState" /> is null without a session and
    ///     <paramref name="frontWaiting" /> is null without a front customer. A customer who stopped waiting
    ///     outranks unscanned items.
    /// </summary>
    public static PaymentReadiness Evaluate(bool registerUsable, CheckoutState? sessionState, bool? frontWaiting) =>
        (registerUsable, sessionState, frontWaiting) switch
        {
            (false, _, _) => PaymentReadiness.NoRegister,
            (_, null, _) => PaymentReadiness.NoSale,
            (_, _, null) => PaymentReadiness.NoSale,
            (_, _, false) => PaymentReadiness.CustomerLeft,
            (_, CheckoutState.Scanning, _) => PaymentReadiness.ScanFirst,
            _ => PaymentReadiness.Ready,
        };

    /// <summary>The message the player sees for a refusal, or null when nothing needs saying.</summary>
    public static string? MessageFor(PaymentReadiness readiness) => readiness switch
    {
        PaymentReadiness.NoRegister => UnavailableMessage,
        PaymentReadiness.ScanFirst => ScanFirstMessage,
        _ => null,
    };
}
