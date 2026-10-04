#if TOOLS
namespace CardCleaner.Addons.TileEditor;

internal static class TmxAtlasNames
{
    internal static string ToSnakeCase(string name)
        => name.ToLowerInvariant().Replace(" ", "_");
}
#endif
