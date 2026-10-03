namespace CardCleaner.Scripts.Features.Deckbuilder.Services.FrontierExploration;

/// <summary>
/// An unvisited cell reached by walking through visited territory, with its walking distance.
/// </summary>
internal readonly record struct FrontierCell(int CellId, int WalkingDistance);
