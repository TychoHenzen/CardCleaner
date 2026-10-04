using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Models;

/// <summary>
/// Owns the red enemy marker sprites drawn into the world map viewport.
/// </summary>
internal sealed class EnemySpriteLayer
{
    private readonly SubViewport? _viewport;
    private readonly List<Sprite2D> _sprites = new();

    internal EnemySpriteLayer(SubViewport? viewport)
    {
        _viewport = viewport;
    }

    internal void Clear()
    {
        foreach (var sprite in _sprites) sprite?.QueueFree();
        _sprites.Clear();
    }

    internal void Create(List<Vector2I> enemyPositions, int tileSize)
    {
        foreach (var pos in enemyPositions)
        {
            var enemySprite = new Sprite2D();
            enemySprite.Texture = CreateColorTexture(Colors.Red, tileSize);
            enemySprite.Position = new Vector2(pos.X * tileSize + tileSize / 2, pos.Y * tileSize + tileSize / 2);
            enemySprite.ZIndex = 200;

            _viewport?.AddChild(enemySprite);
            _sprites.Add(enemySprite);
        }
    }

    internal void RemoveAt(Vector2I position, int tileSize)
    {
        var spriteToRemove = _sprites.FirstOrDefault(sprite =>
        {
            var spriteGridPos = new Vector2I(
                Mathf.RoundToInt((sprite.Position.X - tileSize / 2f) / tileSize),
                Mathf.RoundToInt((sprite.Position.Y - tileSize / 2f) / tileSize)
            );
            return spriteGridPos == position;
        });

        if (spriteToRemove != null)
        {
            _sprites.Remove(spriteToRemove);
            spriteToRemove.QueueFree();
        }
    }

    private static ImageTexture CreateColorTexture(Color color, int size)
    {
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        image.Fill(color);
        return ImageTexture.CreateFromImage(image);
    }
}
