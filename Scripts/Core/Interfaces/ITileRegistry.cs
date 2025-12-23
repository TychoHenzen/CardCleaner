using System.Collections.Generic;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;

namespace CardCleaner.Scripts.Core.Interfaces;

public interface ITileRegistry
{
    void RegisterTile(TileDefinition tile);
    TileDefinition? GetTile(string id);
    IEnumerable<TileDefinition> GetAllTiles();
    void Clear();
}
