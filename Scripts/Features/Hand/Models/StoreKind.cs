namespace CardCleaner.Scripts.Features.Hand.Models;

/// <summary>The kinds of store a click can aim at, each with its own take policy and free space.</summary>
public enum StoreKind
{
    Pile,
    Deck,
    ShelfLevel,
    OpenBox
}
