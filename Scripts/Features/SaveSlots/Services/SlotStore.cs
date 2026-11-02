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
[Service(typeof(SlotStore))]
public sealed class SlotStore(ISlotFileSystem files)
{
    /// <summary>
    ///     Writes the payload as the slot's save. Returns false, and logs the error, when any file step fails.
    /// </summary>
    public bool Save(SaveSlotName name, string payload, DateTime utcNow)
    {
        try
        {
            files.EnsureFolder(SaveSlotLayout.FolderFor(name));
            files.WriteAllText(SaveSlotLayout.TempFor(name), SaveEnvelope.Write(payload, utcNow));
            files.Rename(SaveSlotLayout.TempFor(name), SaveSlotLayout.FileFor(name));
            return true;
        }
        catch (Exception ex)
        {
            // ASSUMPTION: any exception from the file seam counts as a failed save, because the seam wraps the
            // Godot errors and this method must return false rather than leave a half-written slot.
            ILog.Error($"Could not save slot '{name.Value}': {ex.Message}");
            return false;
        }
    }

    /// <summary>
    ///     Reads the slot's save. A refused file is logged and returned; it is never changed.
    /// </summary>
    public LoadResult Load(SaveSlotName name)
    {
        var file = SaveSlotLayout.FileFor(name);
        if (!files.Exists(file))
        {
            return LoadResult.Missing;
        }

        var result = SaveEnvelope.Read(files.ReadAllText(file));
        if (result.Envelope is { } envelope)
        {
            return LoadResult.Loaded(envelope);
        }

        ILog.Error($"Slot '{name.Value}' was not loaded: {result.Refusal}.");
        return LoadResult.Refused(result.Refusal ?? "unreadable");
    }

    /// <summary>
    ///     The slots on disk. Folders with invalid names are skipped. Readable slots come first, newest save first;
    ///     the rest follow in name order.
    /// </summary>
    public IReadOnlyList<SaveSlotName> List()
    {
        var entries = new List<(SaveSlotName Name, DateTime? SavedAt)>();
        foreach (var folder in files.ListFolders(SaveSlotLayout.Root))
        {
            if (!SaveSlotName.TryCreate(folder, out var name, out _))
            {
                continue;
            }

            entries.Add((name, ReadSavedAt(name)));
        }

        return entries
            .OrderBy(entry => entry.SavedAt is null)
            .ThenByDescending(entry => entry.SavedAt ?? DateTime.MinValue)
            .ThenBy(entry => entry.Name.Key, StringComparer.Ordinal)
            .Select(entry => entry.Name)
            .ToList();
    }

    /// <summary>
    ///     Deletes the slot's save file, its temp file and its folder.
    /// </summary>
    public void Delete(SaveSlotName name)
    {
        files.Delete(SaveSlotLayout.TempFor(name));
        files.Delete(SaveSlotLayout.FileFor(name));
        files.Delete(SaveSlotLayout.FolderFor(name));
    }

    private DateTime? ReadSavedAt(SaveSlotName name)
    {
        var file = SaveSlotLayout.FileFor(name);
        if (!files.Exists(file))
        {
            return null;
        }

        var result = SaveEnvelope.Read(files.ReadAllText(file));
        if (result.Envelope is null)
        {
            return null;
        }

        return DateTime.TryParse(
            result.Envelope.SavedAtUtc,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
            out var savedAt)
            ? savedAt
            : null;
    }
}
