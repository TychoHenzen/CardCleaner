using System.Collections.Generic;
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

        var playerStartPos = _mapData.GetCellCenter(playerStartCell);

        // Find all passable cells that are far enough from player start
        var eligibleCells = new List<int>();
        for (int i = 0; i < _mapData.CellCount; i++)
        {
            if (!_mapData.IsPassable(i))
                continue;

            var cellPos = _mapData.GetCellCenter(i);
            var distance = cellPos.DistanceTo(playerStartPos);

            if (distance >= minDistanceFromPlayer)
                eligibleCells.Add(i);
        }

        if (eligibleCells.Count == 0)
        {
            GD.PrintErr("[IrregularMapEnemyManager] No cells far enough from player for enemy placement!");
            // Fallback: use any passable cell that isn't the player start
            for (int i = 0; i < _mapData.CellCount; i++)
            {
                if (_mapData.IsPassable(i) && i != playerStartCell)
                    eligibleCells.Add(i);
            }
        }

        if (eligibleCells.Count == 0)
        {
            GD.PrintErr("[IrregularMapEnemyManager] Not enough passable cells for enemies!");
            return;
        }

        // Shuffle eligible cells
        for (int i = eligibleCells.Count - 1; i > 0; i--)
        {
            int j = (int)(rng.Randi() % (uint)(i + 1));
            (eligibleCells[i], eligibleCells[j]) = (eligibleCells[j], eligibleCells[i]);
        }

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
            var enemyTexture = CreateColoredTexture(Colors.Red, 12, 12);
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

    private static ImageTexture CreateColoredTexture(Color color, int width, int height)
    {
        var image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        image.Fill(color);
        return ImageTexture.CreateFromImage(image);
    }
}
