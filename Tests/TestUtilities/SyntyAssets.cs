using Godot;

namespace CardCleaner.Tests.TestUtilities;

/// <summary>Checks that pack assets exist in the Assets submodule so tests compare files, not text.</summary>
public static class SyntyAssets
{
    public const string Root = "res://Assets/Synty/";

    public static bool Exists(string target) => FileAccess.FileExists(Root + target);
}
