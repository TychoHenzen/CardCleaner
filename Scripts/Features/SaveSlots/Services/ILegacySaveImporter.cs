namespace CardCleaner.Scripts.Features.SaveSlots.Services;

/// <summary>
///     Brings the save from before slots existed into a slot. Runs at most once per start, before the slot list is
///     shown.
/// </summary>
public interface ILegacySaveImporter
{
    /// <summary>
    ///     Imports the legacy save when no slot exists yet. Returns true only when the import slot was written.
    /// </summary>
    bool TryImport();
}
