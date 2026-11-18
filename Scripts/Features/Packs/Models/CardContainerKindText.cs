namespace CardCleaner.Scripts.Features.Packs.Models;

/// <summary>The words the player reads for each kind of container.</summary>
public static class CardContainerKindText
{
    /// <summary>
    ///     The kind's name in lower case: "cardboard box", "booster box" or "booster pack". Only for text the player
    ///     reads; ids and save keys use <c>ProductPriceList.IdOf</c>, so this wording may change.
    /// </summary>
    public static string NameOf(CardContainerKind kind) => kind switch
    {
        CardContainerKind.Box => "cardboard box",
        CardContainerKind.Pack => "booster box",
        _ => "booster pack",
    };
}
