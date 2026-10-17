using System.Collections.Generic;
using CardCleaner.Scripts.Features.Card.Models;

namespace CardCleaner.Scripts.Features.Worldgen.Biomes;

/// <summary>Read access to the registered biomes, so consumers do not depend on the concrete registry.</summary>
public interface IBiomeCatalog
{
    int Count { get; }

    BiomeDefinition? GetBiome(string biomeId);

    IEnumerable<BiomeDefinition> GetAllBiomes();

    IEnumerable<string> GetAllBiomeIds();

    BiomeDefinition? FindClosestBySignature(CardSignature signature);
}
