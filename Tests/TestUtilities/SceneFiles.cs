using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CardCleaner.Tests.TestUtilities;

/// <summary>The scene files of the project, for the suites that check every shipped scene.</summary>
public static class SceneFiles
{
    /// <summary>Every <c>.tscn</c> under <paramref name="folder" /> (a res:// path), sub-folders first.</summary>
    public static List<string> Under(string folder)
    {
        var paths = new List<string>();
        using var directory = DirAccess.Open(folder);
        if (directory == null)
            return paths;

        foreach (var sub in directory.GetDirectories())
            paths.AddRange(Under($"{folder}/{sub}"));

        paths.AddRange(directory.GetFiles().Where(file => file.EndsWith(".tscn")).Select(file => $"{folder}/{file}"));
        return paths;
    }
}
