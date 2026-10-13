using System.Collections.Generic;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh.WorldMap;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

/// <summary>
/// Draws the enemy sprites on the irregular mesh map. The generator places the enemies (MapPopulation).
/// </summary>
public class IrregularMapEnemyManager
{
    private readonly IrregularMeshMapData _mapData;
    private readonly Node _parent;
    private readonly List<(int CellId, Sprite2D Sprite)> _enemySprites = new();
    private IrregularMeshFogOfWar? _fogOfWar;

    public IrregularMapEnemyManager(IrregularMeshMapData mapData, Node parent)
    {
        _mapData = mapData;
        _parent = parent;
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
            _enemySprites.Add((enemyCellId, enemySprite));
        }

        GD.Print($"[IrregularMapEnemyManager] Created {_enemySprites.Count} enemy sprites");
    }

    /// <summary>
    /// Shows enemy sprites only while their cell is currently visible, so they never draw above the fog.
    /// </summary>
    internal void BindFog(IrregularMeshFogOfWar fogOfWar)
    {
        UnbindFog();
        _fogOfWar = fogOfWar;
        _fogOfWar.VisibilityChanged += OnVisibilityChanged;
        ApplyFogVisibility();
    }

    /// <summary>
    /// Stops following fog changes. Sprites keep their current visibility.
    /// </summary>
    private void UnbindFog()
    {
        if (_fogOfWar != null)
            _fogOfWar.VisibilityChanged -= OnVisibilityChanged;
        _fogOfWar = null;
    }

    private void OnVisibilityChanged(IReadOnlySet<int> changedCells) => ApplyFogVisibility();

    private void ApplyFogVisibility()
    {
        if (_fogOfWar == null)
            return;

        foreach (var (cellId, sprite) in _enemySprites)
            sprite.Visible = _fogOfWar.IsVisible(cellId);
    }

    /// <summary>
    /// Remove enemy sprite at the given cell.
    /// </summary>
    public void RemoveEnemyAt(int cellId)
    {
        var spriteIndex = _enemySprites.FindIndex(s =>
            s.Sprite.Position == _mapData.GetCellCenter(cellId));

        if (spriteIndex >= 0 && spriteIndex < _enemySprites.Count)
        {
            _enemySprites[spriteIndex].Sprite.QueueFree();
            _enemySprites.RemoveAt(spriteIndex);
        }
    }

    /// <summary>
    /// Clear all enemy sprites.
    /// </summary>
    public void ClearSprites()
    {
        UnbindFog();
        foreach (var (_, sprite) in _enemySprites)
            sprite?.QueueFree();
        _enemySprites.Clear();
    }

    /// <summary>
    /// Number of enemy sprites currently displayed.
    /// </summary>
    public int SpriteCount => _enemySprites.Count;
}
