namespace CardCleaner.Scripts.Features.Packs.Models;

/// <summary>
///     A booster pack or booster box: the scene it is built from, the seed of what it opens into (null when it draws
///     from the shared random source), and the price the player set on it.
/// </summary>
public sealed record StoredContainer(string ScenePath, CardContainerKind Kind, ulong? Seed, int? Price) : StoredItem
{
    public override ItemShape Shape => Kind == CardContainerKind.Pack ? ItemShape.Pack : ItemShape.Booster;
}
