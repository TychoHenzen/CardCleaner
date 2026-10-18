using System;
using System.IO;
using CardCleaner.Scripts.Core.Interfaces;

namespace CardCleaner.Tests.TestUtilities;

/// <summary>
/// Maps res:// paths onto a directory on disk, so a test can load data from a fake project root.
/// </summary>
internal sealed class TempAssetPaths(string root) : IAssetPaths
{
    /// <summary>
    /// Creates a new empty directory under the system temp path, named with a fresh GUID.
    /// </summary>
    public static string CreateTempRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    public string Globalize(string resPath)
    {
        const string prefix = "res://";
        if (!resPath.StartsWith(prefix, StringComparison.Ordinal))
            return resPath;

        return Path.Combine(root, resPath.Substring(prefix.Length).Replace('/', Path.DirectorySeparatorChar));
    }
}
