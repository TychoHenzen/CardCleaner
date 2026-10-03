namespace CardCleaner.Scripts.Features.Deckbuilder.Services;

public enum SessionState
{
    WaitingForCards,
    GeneratingMap,
    Exploring,
    InCombat,
    GeneratingLoot,
    SessionComplete
}
