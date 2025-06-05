using Godot;
using Godot.Collections;

namespace CardCleaner.Scripts.Features.Worldgen;

public interface IWorldGenerator
{
    void Generate(uint seed, Vector2I mapSize, Array<SemanticTile> semanticTiles, 
        TileMapLayer? terrainLayer, TileMapLayer? structureLayer, 
        TileMapLayer? decorationLayer, TileMapLayer? effectLayer);
}