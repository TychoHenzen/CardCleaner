namespace CardCleaner.Scripts.Features.Packs.Models;

/// <summary>
///     One item as data instead of a body in the world, enough to build it again exactly as it was: what a cardboard
///     box keeps, or the cardboard box itself while a move carries it (<see cref="StoredBox" />).
/// </summary>
public abstract record StoredItem
{
    /// <summary>The shape the item takes when it is packed.</summary>
    public abstract ItemShape Shape { get; }
}
