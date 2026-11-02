using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.SaveSlots.Models;

namespace CardCleaner.Scripts.Features.SaveSlots.Services;

/// <summary>
///     Saves, loads, lists and deletes slots. A save is written to a temp file and then renamed over the save file,
///     so a failed write leaves the previous save in place.
/// </summary>
[Service(typeof(SlotStore), typeof(ISaveSlotCatalog))]
public sealed class SlotStore : ISaveSlotCatalog
{
    private readonly ISlotFileSystem _files;

    public SlotStore(ISlotFileSystem files)
    {
        _files = files;
    }

    /// <summary>
    ///     Writes the payload as the slot's save. Returns false, and logs the error, when any file step fails; a slot
    ///     that had no save yet is then removed again.
    /// </summary>
    public bool Save(SaveSlotName name, string payload, DateTime utcNow)
    {
        try
        {
            _files.EnsureFolder(SaveSlotLayout.FolderFor(name));
            _files.WriteAllText(SaveSlotLayout.TempFor(name), SaveEnvelope.Write(payload, utcNow));
            _files.Rename(SaveSlotLayout.TempFor(name), SaveSlotLayout.FileFor(name));
            return true;
        }
        catch (Exception ex)
        {
            // ASSUMPTION: any exception from the file seam counts as a failed save, because the seam wraps the
            // Godot errors and this method must return false rather than leave a half-written slot.
            ILog.Error($"Could not save slot '{name.Value}': {ex.Message}");
            RemoveSlotWithoutSave(name);
            return false;
        }
    }

    // A first save that fails after its folder was made would leave an empty slot behind, which then counts as an
    // existing slot. A slot that already holds a save keeps it.
    private void RemoveSlotWithoutSave(SaveSlotName name)
    {
        try
        {
            if (!_files.Exists(SaveSlotLayout.FileFor(name)))
            {
                Delete(name);
            }
        }
        catch (Exception cleanup)
        {
            ILog.Error($"Could not remove the empty slot '{name.Value}': {cleanup.Message}");
        }
    }

    /// <summary>
    ///     Reads the slot's save. A refused file is logged and returned; it is never changed.
    /// </summary>
    public LoadResult Load(SaveSlotName name)
    {
        var file = SaveSlotLayout.FileFor(name);
        if (!_files.Exists(file))
        {
            return LoadResult.Missing;
        }

        var result = SaveEnvelope.Read(_files.ReadAllText(file));
        if (result.Envelope is { } envelope)
        {
            return LoadResult.Loaded(envelope);
        }

        ILog.Error($"Slot '{name.Value}' was not loaded: {result.Refusal}.");
        return LoadResult.Refused(result.Refusal ?? "unreadable");
    }

    /// <summary>
    ///     The slots on disk. Folders with invalid names are skipped. Readable slots come first, newest save first, a
    ///     readable slot without a parsable time after those with one; unreadable slots follow. Ties are broken by
    ///     name.
    /// </summary>
    public IReadOnlyList<SaveSlotInfo> List()
    {
        var entries = new List<SaveSlotInfo>();
        foreach (var folder in _files.ListFolders(SaveSlotLayout.Root))
        {
            if (!SaveSlotName.TryCreate(folder, out var name, out _))
            {
                continue;
            }

            entries.Add(ReadInfo(name));
        }

        return entries
            .OrderBy(entry => !entry.IsReadable)
            .ThenBy(entry => entry.SavedAtUtc is null)
            .ThenByDescending(entry => entry.SavedAtUtc ?? DateTime.MinValue)
            .ThenBy(entry => entry.Name.Key, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    ///     Creates an empty slot folder for the name. Refuses an invalid name, or a name that matches an existing slot
    ///     ignoring case. Writes no save file, so the new slot is listed as unreadable until its first save.
    /// </summary>
    public bool TryCreate(string? rawName, out SaveSlotName name, out string reason)
    {
        if (!SaveSlotName.TryCreate(rawName, out name, out reason))
        {
            return false;
        }

        var folderKey = name.Folder;
        var exists = _files.ListFolders(SaveSlotLayout.Root)
            .Any(folder => string.Equals(folder, folderKey, StringComparison.Ordinal));
        if (exists)
        {
            reason = $"A slot named '{name.Value}' already exists.";
            return false;
        }

        try
        {
            _files.EnsureFolder(SaveSlotLayout.FolderFor(name));
        }
        catch (Exception ex)
        {
            ILog.Error($"Could not create slot '{name.Value}': {ex.Message}");
            reason = "The slot could not be created.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    /// <summary>
    ///     Deletes the slot's save file, its temp file and its folder.
    /// </summary>
    public void Delete(SaveSlotName name)
    {
        _files.Delete(SaveSlotLayout.TempFor(name));
        _files.Delete(SaveSlotLayout.FileFor(name));
        _files.Delete(SaveSlotLayout.FolderFor(name));
    }

    private SaveSlotInfo ReadInfo(SaveSlotName name)
    {
        var file = SaveSlotLayout.FileFor(name);
        if (!_files.Exists(file))
        {
            return new SaveSlotInfo(name, null, false);
        }

        var result = SaveEnvelope.Read(_files.ReadAllText(file));
        if (result.Envelope is null)
        {
            return new SaveSlotInfo(name, null, false);
        }

        DateTime? savedAt = DateTime.TryParse(
            result.Envelope.SavedAtUtc,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
            out var parsed)
            ? parsed
            : null;
        return new SaveSlotInfo(name, savedAt, true);
    }
}
