using System;
using System.Collections.Generic;
using System.IO;
using CardCleaner.Scripts.Core.DependencyInjection;
using Godot;
using FileAccess = Godot.FileAccess;

namespace CardCleaner.Scripts.Features.SaveSlots.Services;

/// <summary>
///     The game's <see cref="ISlotFileSystem" />, built on Godot's FileAccess and DirAccess. Paths are converted to
///     absolute system paths for the rename and delete calls, which only take absolute paths.
/// </summary>
[Service(typeof(ISlotFileSystem))]
internal sealed class GodotSlotFileSystem : ISlotFileSystem
{
    public bool Exists(string path) => FileAccess.FileExists(path);

    public string? ReadAllText(string path)
    {
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        return file?.GetAsText();
    }

    public void WriteAllText(string path, string text)
    {
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write)
            ?? throw new IOException($"Could not open {path} for writing: {FileAccess.GetOpenError()}.");
        file.StoreString(text);

        var error = file.GetError();
        if (error != Error.Ok)
        {
            throw new IOException($"Could not write {path}: {error}.");
        }
    }

    /// <summary>
    ///     Moves the file over the target in one replace step (MoveFileEx with replace-existing on Windows, rename(2)
    ///     elsewhere), so the target is never deleted first and a failed move leaves the old target in place.
    /// </summary>
    public void Rename(string from, string destination) =>
        File.Move(ProjectSettings.GlobalizePath(from), ProjectSettings.GlobalizePath(destination), overwrite: true);

    public void Delete(string path)
    {
        var absolute = ProjectSettings.GlobalizePath(path);
        if (!FileAccess.FileExists(path) && !DirAccess.DirExistsAbsolute(absolute))
        {
            return;
        }

        var error = DirAccess.RemoveAbsolute(absolute);
        if (error != Error.Ok)
        {
            throw new IOException($"Could not delete {path}: {error}.");
        }
    }

    public IReadOnlyList<string> ListFolders(string path)
    {
        using var dir = DirAccess.Open(path);
        return dir == null ? Array.Empty<string>() : dir.GetDirectories();
    }

    public void EnsureFolder(string path)
    {
        var absolute = ProjectSettings.GlobalizePath(path);
        if (DirAccess.DirExistsAbsolute(absolute))
        {
            return;
        }

        var error = DirAccess.MakeDirRecursiveAbsolute(absolute);
        if (error != Error.Ok)
        {
            throw new IOException($"Could not create {path}: {error}.");
        }
    }
}
