using System.Collections.Generic;

namespace CardCleaner.Scripts.Features.SaveSlots.Services;

/// <summary>
///     The file operations the slot store needs. Paths are Godot paths such as <c>user://saves/my shop/save.json</c>.
///     The store is tested against an in-memory implementation; the game uses the Godot one.
/// </summary>
public interface ISlotFileSystem
{
    bool Exists(string path);

    /// <summary>The file's text, or null when the file does not exist.</summary>
    string? ReadAllText(string path);

    void WriteAllText(string path, string text);

    /// <summary>Moves a file to a new path, replacing the target when it exists.</summary>
    void Rename(string from, string to);

    /// <summary>Deletes a file, or an empty folder. Does nothing when the path does not exist.</summary>
    void Delete(string path);

    /// <summary>The names (not full paths) of the folders directly under the path; empty when it does not exist.</summary>
    IReadOnlyList<string> ListFolders(string path);

    void EnsureFolder(string path);
}
