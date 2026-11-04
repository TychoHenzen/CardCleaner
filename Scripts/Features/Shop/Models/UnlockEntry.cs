namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>One unlockable thing: its catalog id, what it is, and the shop level that unlocks it.</summary>
public sealed record UnlockEntry(string Id, UnlockKind Kind, int RequiredLevel);
