using System.Collections.Generic;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Components;

/// <summary>
/// Manages fog of war sprites for the world map.
/// Creates and updates fog sprites based on visibility state.
/// </summary>
public class WorldMapFogManager
{
    private readonly Dictionary<Vector2I, Sprite2D> _fogSprites = new();
    private ImageTexture? _fogTexture;
    private readonly int _tileSize;
    private readonly Node _parent;

    public WorldMapFogManager(Node parent, int tileSize)
    {
        _parent = parent;
        _tileSize = tileSize;
    }

    /// <summary>
    /// Initialize fog of war for a map of the given size.
    /// </summary>
    public void Initialize(Vector2I mapSize)
    {
        Clear();

        // Create black fog texture if not already created
        _fogTexture ??= CreateColorTexture(new Color(0, 0, 0, 1), _tileSize);

        // Create fog sprites for all tiles (90% opacity so map is barely visible)
        for (var y = 0; y < mapSize.Y; y++)
        for (var x = 0; x < mapSize.X; x++)
        {
            var position = new Vector2I(x, y);
            var sprite = new Sprite2D
            {
                Texture = _fogTexture,
                Position = new Vector2(x * _tileSize + _tileSize / 2, y * _tileSize + _tileSize / 2),
                ZIndex = 50, // Below UI (which uses CanvasLayer)
                Modulate = new Color(1, 1, 1, 0.9f) // 90% opacity - map barely visible through fog
            };

            _parent.AddChild(sprite);
            _fogSprites[position] = sprite;
        }
    }

    /// <summary>
    /// Update fog of war visibility based on seen and currently visible tiles.
    /// </summary>
    public void UpdateVisibility(IReadOnlySet<Vector2I> seenTiles, IReadOnlySet<Vector2I> currentlyVisibleTiles)
    {
        foreach (var (position, sprite) in _fogSprites)
        {
            if (currentlyVisibleTiles.Contains(position))
            {
                // Currently visible: fully transparent (no fog)
                sprite.Modulate = new Color(1, 1, 1, 0);
            }
            else if (seenTiles.Contains(position))
            {
                // Previously seen but not currently visible: 50% fog
                sprite.Modulate = new Color(1, 1, 1, 0.5f);
            }
            // else: Never seen - stays at 90% opacity (set in Initialize)
        }
    }

    /// <summary>
    /// Reveal the entire map (remove all fog).
    /// </summary>
    public void RevealAll()
    {
        foreach (var sprite in _fogSprites.Values)
            sprite.Modulate = new Color(1, 1, 1, 0);
    }

    /// <summary>
    /// Clear all fog sprites.
    /// </summary>
    public void Clear()
    {
        foreach (var sprite in _fogSprites.Values)
            sprite?.QueueFree();
        _fogSprites.Clear();
    }

    /// <summary>
    /// Number of fog sprites currently managed.
    /// </summary>
    public int SpriteCount => _fogSprites.Count;

    private static ImageTexture CreateColorTexture(Color color, int size)
    {
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        image.Fill(color);
        return ImageTexture.CreateFromImage(image);
    }
}
