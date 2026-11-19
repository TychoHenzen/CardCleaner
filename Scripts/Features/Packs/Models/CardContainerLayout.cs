namespace CardCleaner.Scripts.Features.Packs.Models;

/// <summary>
///     The fixed shape of the container hierarchy: every container holds <see cref="ItemsPerContainer" />
///     items, so a box holds 8 packs, a pack 8 boosters and a booster 8 cards (512 cards per box).
/// </summary>
public static class CardContainerLayout
{
    public const int ItemsPerContainer = 8;

    public const int CardsPerBox = ItemsPerContainer * ItemsPerContainer * ItemsPerContainer;
}
