using System;

namespace CardCleaner.Scripts.Features.Shop.Furniture;

/// <summary>
///     What the placement keys do: turn the piece, confirm, cancel, pick a placed piece up again, and pack a placed
///     piece back into its box.
/// </summary>
internal sealed record PlacementActions(Action Rotate, Action Confirm, Action Cancel, Action PickUp, Action Pack);
