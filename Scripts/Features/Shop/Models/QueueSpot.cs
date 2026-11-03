namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>A point in the shop where a waiting customer stands, in shop world coordinates.</summary>
public readonly record struct QueueSpot(float X, float Y, float Z);
