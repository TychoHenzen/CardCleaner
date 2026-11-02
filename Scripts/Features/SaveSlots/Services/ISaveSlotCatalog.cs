using System.Collections.Generic;
using CardCleaner.Scripts.Features.SaveSlots.Models;

namespace CardCleaner.Scripts.Features.SaveSlots.Services;

/// <summary>
///     The slots on disk, for the main menu. It lists, creates and deletes slots; it does not load them, because the
///     menu selects a slot through <see cref="IActiveSaveSlot" />.
/// </summary>
public interface ISaveSlotCatalog
{
    /// <summary>
    ///     The slots, readable ones first with the newest save first, then the unreadable ones. Folders whose names are
    ///     not valid slot names are skipped.
    /// </summary>
    IReadOnlyList<SaveSlotInfo> List();

    /// <summary>
    ///     Creates an empty slot named by the text. Returns false with the reason when the name is not allowed or a slot
    ///     with the same name (ignoring case) exists. Writes no save file.
    /// </summary>
    bool TryCreate(string? rawName, out SaveSlotName name, out string reason);

    /// <summary>Deletes the slot's save file, its temp file and its folder.</summary>
    void Delete(SaveSlotName name);
}
