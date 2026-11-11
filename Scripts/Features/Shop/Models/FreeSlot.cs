namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>One empty shelf slot: its id, the shelf it belongs to and that shelf's kind.</summary>
public sealed record FreeSlot(int Id, int ShelfId, ShelfKind Shelf);
