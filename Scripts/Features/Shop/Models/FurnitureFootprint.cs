namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>
///     The floor size of a piece of furniture at zero quarter turns: Width along X, Depth along Z, in metres.
///     Pure data.
/// </summary>
public readonly record struct FurnitureFootprint(float Width, float Depth);
