using System;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>What the placement keys do: turn the piece, confirm, cancel, and pick a placed piece up again.</summary>
internal sealed record PlacementActions(Action Rotate, Action Confirm, Action Cancel, Action PickUp);
