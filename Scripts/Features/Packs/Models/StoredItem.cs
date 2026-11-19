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
