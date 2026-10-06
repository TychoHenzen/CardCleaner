using System.Linq;
using System.Text.Json;
using Godot;

namespace CardCleaner.Tests.TestUtilities;

/// <summary>Reads the pack asset manifest as data so tests compare targets, not text.</summary>
public static class SyntyManifest
{
    public const string Path = "res://tools/synty-assets.json";

    public static bool ListsTarget(string target)
    {
        using var document = JsonDocument.Parse(FileAccess.GetFileAsString(Path));
        return document.RootElement.GetProperty("files").EnumerateArray()
            .Any(file => file.GetProperty("target").GetString() == target);
    }
}
