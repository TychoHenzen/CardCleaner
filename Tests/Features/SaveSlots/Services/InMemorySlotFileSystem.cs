using System;
using System.Collections.Generic;
using System.IO;
using CardCleaner.Scripts.Features.SaveSlots.Services;

namespace CardCleaner.Tests.Features.SaveSlots.Services;

/// <summary>
///     An in-memory <see cref="ISlotFileSystem" /> for the slot store tests. Failures are switched on per test.
/// </summary>
public sealed class InMemorySlotFileSystem : ISlotFileSystem
{
    private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);
    private readonly HashSet<string> _folders = new(StringComparer.Ordinal);

    /// <summary>The number of WriteAllText calls made so far.</summary>
    public int WriteCalls { get; private set; }

    /// <summary>The 1-based WriteAllText call that throws, or -1 for none.</summary>
    public int FailWriteOnCall { get; set; } = -1;

    public bool FailRename { get; set; }

    public bool Exists(string path) => _files.ContainsKey(path);

    public string? ReadAllText(string path) => _files.TryGetValue(path, out var text) ? text : null;

    public void WriteAllText(string path, string text)
    {
        WriteCalls++;
        if (WriteCalls == FailWriteOnCall)
        {
            throw new IOException($"Injected write failure on call {WriteCalls}.");
        }

        _files[path] = text;
    }

    public void Rename(string from, string to)
    {
        if (FailRename)
        {
            throw new IOException("Injected rename failure.");
        }

        if (!_files.TryGetValue(from, out var text))
        {
            throw new IOException($"No file {from} to rename.");
        }

        _files[to] = text;
        _files.Remove(from);
    }

    public void Delete(string path)
    {
        _files.Remove(path);
        _folders.Remove(path);
    }

    public IReadOnlyList<string> ListFolders(string path)
    {
        var names = new List<string>();
        foreach (var folder in _folders)
        {
            var slash = folder.LastIndexOf('/');
            if (slash > 0 && folder.Substring(0, slash) == path)
            {
                names.Add(folder.Substring(slash + 1));
            }
        }

        return names;
    }

    public void EnsureFolder(string path) => _folders.Add(path);

    /// <summary>Puts raw text at a path, as if another program had written it.</summary>
    public void Seed(string path, string text) => _files[path] = text;

    /// <summary>The raw text at a path, or null.</summary>
    public string? Peek(string path) => ReadAllText(path);
}
