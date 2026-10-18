using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Core.Services;

public sealed class GodotAssetPaths : IAssetPaths
{
    public string Globalize(string resPath) => ProjectSettings.GlobalizePath(resPath);
}
