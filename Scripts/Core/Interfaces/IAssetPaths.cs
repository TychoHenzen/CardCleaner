namespace CardCleaner.Scripts.Core.Interfaces;

public interface IAssetPaths
{
    /// <summary>
    /// Maps a res:// path to a file system path. A path that is not res:// comes back unchanged.
    /// </summary>
    string Globalize(string resPath);
}
