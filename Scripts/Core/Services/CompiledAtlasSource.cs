using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services.CompiledAtlas;
using Godot;

namespace CardCleaner.Scripts.Core.Services;

/// <summary>
/// The production <see cref="ICompiledAtlasSource"/>: answers from <see cref="CompiledAtlasLoader"/>.
/// </summary>
internal sealed class CompiledAtlasSource : ICompiledAtlasSource
{
    public bool IsAvailable(string? mappingPath) => CompiledAtlasLoader.IsCompiledAtlasAvailable(mappingPath);

    public AtlasMapping.AtlasMappingData? LoadMapping(string? mappingPath)
        => CompiledAtlasLoader.LoadMapping(mappingPath);

    public bool CanBuild(string? mappingPath)
    {
        // The tile set is an engine resource: this class is the one place that touches it for the catalog.
        TileSet? tileSet = CompiledAtlasLoader.LoadCompiledTileSet(mappingPath);
        return tileSet != null;
    }
}
