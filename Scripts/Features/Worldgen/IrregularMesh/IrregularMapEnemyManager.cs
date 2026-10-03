using System.Collections.Generic;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh.WorldMap;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

/// <summary>
/// Manages enemy placement and sprites on the irregular mesh map.
/// </summary>
public class IrregularMapEnemyManager
{
    private readonly IrregularMeshMapData _mapData;
    private readonly Node _parent;
    private readonly List<Sprite2D> _enemySprites = new();

    public IrregularMapEnemyManager(IrregularMeshMapData mapData, Node parent)
    {
        _mapData = mapData;
        _parent = parent;
    }

    /// <summary>
    /// Places enemies on the map at a minimum distance from player start.
    /// </summary>
    /// <param name="seed">Random seed for placement.</param>
    /// <param name="playerStartCell">Cell ID where the player starts.</param>
    /// <param name="minDistanceFromPlayer">Minimum world-space distance from player.</param>
    public void PlaceEnemies(int seed, int playerStartCell, float minDistanceFromPlayer = 200f)
    {
        var rng = new RandomNumberGenerator();
        rng.Seed = (ulong)seed;

        var eligibleCells = FindEligibleCells(playerStartCell, minDistanceFromPlayer);
        if (eligibleCells.Count == 0)
        {
            GD.PrintErr("[IrregularMapEnemyManager] Not enough passable cells for enemies!");
            return;
        }

        ShuffleCells(eligibleCells, rng);

        // Place 1-3 enemies
        int enemyCount = rng.RandiRange(1, Mathf.Min(3, eligibleCells.Count));
        for (int i = 0; i < enemyCount; i++)
        {
            _mapData.AddEnemySpawn(eligibleCells[i]);
        }

        GD.Print(
            $"[IrregularMapEnemyManager] Placed {enemyCount} enemies on map " +
            $"(min distance: {minDistanceFromPlayer})");
    }

    /// <summary>
    /// Passable cells far enough from the player start, or any passable cell other than the start as fallback.
    /// </summary>
    private List<int> FindEligibleCells(int playerStartCell, float minDistanceFromPlayer)
    {
        var playerStartPos = _mapData.GetCellCenter(playerStartCell);

        var eligibleCells = new List<int>();
        for (int i = 0; i < _mapData.CellCount; i++)
        {
            if (!_mapData.IsPassable(i))
                continue;

            var distance = _mapData.GetCellCenter(i).DistanceTo(playerStartPos);
            if (distance >= minDistanceFromPlayer)
                eligibleCells.Add(i);
        }

        if (eligibleCells.Count > 0)
            return eligibleCells;

        GD.PrintErr("[IrregularMapEnemyManager] No cells far enough from player for enemy placement!");
        for (int i = 0; i < _mapData.CellCount; i++)
        {
            if (_mapData.IsPassable(i) && i != playerStartCell)
                eligibleCells.Add(i);
        }

        return eligibleCells;
    }

    private static void ShuffleCells(List<int> cells, RandomNumberGenerator rng)
    {
        for (int i = cells.Count - 1; i > 0; i--)
        {
            int j = (int)(rng.Randi() % (uint)(i + 1));
            (cells[i], cells[j]) = (cells[j], cells[i]);
        }
    }

    /// <summary>
    /// Creates visual sprites for all enemy spawn locations.
    /// </summary>
    public void CreateEnemySprites()
    {
        ClearSprites();

        foreach (var enemyCellId in _mapData.EnemySpawnCells)
        {
            var enemySprite = new Sprite2D
            {
                ZIndex = 80 // Below player (100) but above terrain
            };

            // Create red enemy texture
            var enemyTexture = ColoredTextureFactory.Create(Colors.Red, 12, 12);
            enemySprite.Texture = enemyTexture;

            // Position at cell center (GetCellCenter already applies WorldScale)
            enemySprite.Position = _mapData.GetCellCenter(enemyCellId);

            _parent.AddChild(enemySprite);
            _enemySprites.Add(enemySprite);
        }

        GD.Print($"[IrregularMapEnemyManager] Created {_enemySprites.Count} enemy sprites");
    }

    /// <summary>
    /// Remove enemy sprite at the given cell.
    /// </summary>
    public void RemoveEnemyAt(int cellId)
    {
        var spriteIndex = _enemySprites.FindIndex(s =>
            s.Position == _mapData.GetCellCenter(cellId));

        if (spriteIndex >= 0 && spriteIndex < _enemySprites.Count)
        {
            _enemySprites[spriteIndex].QueueFree();
            _enemySprites.RemoveAt(spriteIndex);
        }
    }

    /// <summary>
    /// Clear all enemy sprites.
    /// </summary>
    public void ClearSprites()
    {
        foreach (var sprite in _enemySprites)
            sprite?.QueueFree();
        _enemySprites.Clear();
    }

    /// <summary>
    /// Number of enemy sprites currently displayed.
    /// </summary>
    public int SpriteCount => _enemySprites.Count;
}
