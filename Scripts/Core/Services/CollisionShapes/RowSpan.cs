namespace CardCleaner.Scripts.Core.Services;

/// <summary>
/// A horizontal run of grid cells on one row: the row index, first column and cell count.
/// </summary>
internal readonly record struct RowSpan(int Y, int StartX, int Width);
