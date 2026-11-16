namespace CardCleaner.Scripts.Features.Packs.Models;

/// <summary>The words the player reads for each kind of container.</summary>
public static class CardContainerKindText
{
    /// <summary>The kind's name in lower case: "booster box", "pack" or "booster".</summary>
    public static string NameOf(CardContainerKind kind) => kind switch
    {
        CardContainerKind.Box => "booster box",
        CardContainerKind.Pack => "pack",
        _ => "booster",
    };

    /// <summary>What a kind opens into: a box holds packs, a pack holds boosters and a booster holds cards.</summary>
    public static string ContentsOf(CardContainerKind kind) => kind switch
    {
        CardContainerKind.Box => "packs",
        CardContainerKind.Pack => "boosters",
        _ => "cards",
    };
}
