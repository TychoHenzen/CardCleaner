namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>One named charge on a day's bills.</summary>
public sealed record BillLine(string Name, int Amount);
