using System;
using System.Text.Json;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.SaveSlots.Models;

namespace CardCleaner.Scripts.Features.SaveSlots.Services;

/// <summary>
///     Copies the single-file save from before slots existed into a slot named "imported". It runs only when no slot
///     exists yet and the legacy file holds a JSON object. The legacy file is only read, never changed.
/// </summary>
// ASSUMPTION: a bare [Service] registers under interfaces only, and this class has none, so it names its own type,
// as SlotStore does.
[Service(typeof(LegacySaveImporter), typeof(ILegacySaveImporter))]
public sealed class LegacySaveImporter(SlotStore store, ISlotFileSystem files, IActiveSaveSlot activeSlot)
    : ILegacySaveImporter
{
    /// <summary>The path GameSaveService wrote before save slots.</summary>
    public const string LegacyPath = "user://game_save.json";

    private const string ImportedSlotKey = "imported";

    /// <summary>
    ///     Imports the legacy save when there are no slots and the legacy file exists. Returns true only when the
    ///     import slot was written; then the import slot becomes the active slot, so the first load after the upgrade
    ///     reads the player's old save. When nothing is imported the active slot is left unchanged.
    /// </summary>
    public bool TryImport()
    {
        if (store.List().Count > 0 || !files.Exists(LegacyPath))
        {
            return false;
        }

        var text = files.ReadAllText(LegacyPath);
        if (text is null)
        {
            ILog.Warning($"The legacy save {LegacyPath} could not be read; it was not imported.");
            return false;
        }

        if (!IsJsonDocument(text))
        {
            ILog.Warning($"The legacy save {LegacyPath} is empty or not valid JSON; it was not imported.");
            return false;
        }

        SaveSlotName.TryCreate(ImportedSlotKey, out var name, out _);
        if (!store.Save(name, text, DateTime.UtcNow))
        {
            return false;
        }

        activeSlot.Activate(name);
        return true;
    }

    private static bool IsJsonDocument(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            return document.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
