namespace CardCleaner.Scripts.Features.Packs.Models;

/// <summary>The words the player reads for each kind of container.</summary>
public static class CardContainerKindText
{
    /// <summary>
    ///     The kind's name in lower case: "cardboard box", "booster box" or "booster pack". Only for text the player
    ///     reads; ids use <c>ProductIds.IdOf</c>, so this wording may change.
    /// </summary>
    public static string NameOf(CardContainerKind kind) => kind switch
    {
        CardContainerKind.Box => "cardboard box",
        CardContainerKind.Pack => "booster box",
        _ => "booster pack",
    };

    /// <summary>The kind's name for more than one: "cardboard boxes", "booster boxes" or "booster packs".</summary>
    public static string PluralOf(CardContainerKind kind) => kind switch
    {
        CardContainerKind.Box => "cardboard boxes",
        CardContainerKind.Pack => "booster boxes",
        _ => "booster packs",
    };
}
