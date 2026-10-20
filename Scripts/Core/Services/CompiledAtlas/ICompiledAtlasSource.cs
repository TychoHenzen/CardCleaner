namespace CardCleaner.Scripts.Core.Services.CompiledAtlas;

/// <summary>
/// Where the tile catalog gets its compiled atlas from. The catalog asks only these three questions, so it does not
/// depend on the loader that owns the engine tile-set resource.
/// </summary>
public interface ICompiledAtlasSource
{
    /// <summary>Whether a compiled atlas mapping exists at the path (null means the default mapping).</summary>
    bool IsAvailable(string? mappingPath);

    /// <summary>The atlas mapping, or null when the file is missing or invalid.</summary>
    AtlasMapping.AtlasMappingData? LoadMapping(string? mappingPath);

    /// <summary>Whether the atlas image behind the mapping loads into a tile set.</summary>
    bool CanBuild(string? mappingPath);
}
