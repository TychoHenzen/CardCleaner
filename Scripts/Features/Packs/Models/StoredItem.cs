using CardCleaner.Scripts.Features.Card.Models;

namespace CardCleaner.Scripts.Features.Packs.Models;

/// <summary>
///     One item kept in a cardboard box as data instead of as a body in the world: enough to build the item again
///     exactly as it was when it went in.
/// </summary>
public abstract record StoredItem
{
    /// <summary>The shape the item takes when it is packed.</summary>
    public abstract ItemShape Shape { get; }
}

/// <summary>
///     A card: its signature (null for a card without one), the seed of its look, and the price the player set on it
///     (null while it asks its market price). The same fields a save writes for a loose card.
/// </summary>
public sealed record StoredCard(CardSignature? Signature, int? LookSeed, int? Price) : StoredItem
{
    public override ItemShape Shape => ItemShape.Card;
}

/// <summary>
///     A booster pack or booster box: the scene it is built from, the seed of what it opens into (null when it draws
///     from the shared random source), and the price the player set on it.
/// </summary>
public sealed record StoredContainer(string ScenePath, CardContainerKind Kind, ulong? Seed, int? Price) : StoredItem
{
    public override ItemShape Shape => Kind == CardContainerKind.Pack ? ItemShape.Pack : ItemShape.Booster;
}
