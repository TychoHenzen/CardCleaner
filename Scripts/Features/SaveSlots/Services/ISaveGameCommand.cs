namespace CardCleaner.Scripts.Features.SaveSlots.Services;

/// <summary>
///     Saves the game into the active slot on request, for the pause menu's Save button.
/// </summary>
public interface ISaveGameCommand
{
    /// <summary>Writes the active slot now. Returns false when the write failed or there is no store to write to.</summary>
    bool ManualSave();
}
