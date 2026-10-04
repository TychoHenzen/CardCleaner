namespace CardCleaner.Scripts.Features.Deckbuilder.Services;

internal readonly record struct TwoPhaseWfcResult(
    string[,] BackgroundLayer,
    string[,] ForegroundLayer,
    string[,] MergedGrid);
