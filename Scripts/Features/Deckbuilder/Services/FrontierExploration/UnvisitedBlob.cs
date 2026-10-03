namespace CardCleaner.Scripts.Features.Deckbuilder.Services.FrontierExploration;

/// <summary>
/// Represents a connected region of unvisited cells.
/// </summary>
internal readonly struct UnvisitedBlob
{
    internal int EntryCellId { get; init; }
    internal int Size { get; init; }
    internal int WalkingDistance { get; init; }
}
