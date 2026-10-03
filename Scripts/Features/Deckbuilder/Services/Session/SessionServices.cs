using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Worldgen.Biomes;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services.Session;

/// <summary>
/// Registries the session needs for map generation. Filled in when the session node becomes ready.
/// </summary>
internal sealed class SessionServices
{
    internal ITileRegistry? TileRegistry { get; set; }
    internal ITileMetadataProvider MetadataProvider { get; set; } = null!;
    internal BiomeRegistry BiomeRegistry { get; set; } = null!;
}
