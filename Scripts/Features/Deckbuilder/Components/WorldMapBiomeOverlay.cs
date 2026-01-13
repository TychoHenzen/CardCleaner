using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Components;

/// <summary>
/// Manages biome overlay visualization for the world map.
/// Creates colored sprites to show biome distribution.
/// </summary>
public class WorldMapBiomeOverlay
{
    private readonly List<Sprite2D> _biomeOverlaySprites = new();
    private readonly Dictionary<string, ImageTexture> _biomeTextures = new();
    private readonly Node _parent;
    private readonly int _tileSize;

    // Biome colors for overlay visualization (semi-transparent)
    private static readonly Dictionary<string, Color> BiomeColors = new()
    {
        { "plains", new Color(0.3f, 0.8f, 0.3f, 0.4f) },   // Green
        { "forest", new Color(0.1f, 0.5f, 0.1f, 0.4f) },   // Dark Green
        { "desert", new Color(0.9f, 0.8f, 0.4f, 0.4f) },   // Sandy Yellow
        { "tundra", new Color(0.7f, 0.9f, 1.0f, 0.4f) },   // Ice Blue
        { "mountains", new Color(0.5f, 0.5f, 0.5f, 0.4f) }, // Grey
        { "swamp", new Color(0.3f, 0.4f, 0.2f, 0.4f) }     // Murky Green
    };

    public WorldMapBiomeOverlay(Node parent, int tileSize)
    {
        _parent = parent;
        _tileSize = tileSize;
    }

    /// <summary>
    /// Render biome overlay for the given map data.
    /// </summary>
    public void Render(SimpleMapData mapData)
    {
        Clear();

        if (mapData.BiomeMap == null)
        {
            ILog.Print("[BIOME OVERLAY] No biome data available");
            return;
        }

        // Count biomes for logging
        var biomeCounts = new Dictionary<string, int>();

        // Create textures for each biome type if not cached
        foreach (var (biomeId, color) in BiomeColors)
            if (!_biomeTextures.ContainsKey(biomeId))
                _biomeTextures[biomeId] = CreateColorTexture(color, _tileSize);

        // Create sprites for each tile position showing biome color
        for (var y = 0; y < mapData.Size.Y; y++)
        for (var x = 0; x < mapData.Size.X; x++)
        {
            var biomeId = mapData.GetBiomeAt(new Vector2I(x, y));

            // Track counts
            biomeCounts.TryGetValue(biomeId, out var count);
            biomeCounts[biomeId] = count + 1;

            // Get or create texture for this biome
            if (!_biomeTextures.TryGetValue(biomeId, out var texture))
            {
                texture = CreateColorTexture(new Color(1, 0, 1, 0.5f), _tileSize); // Magenta fallback
                _biomeTextures[biomeId] = texture;
            }

            var sprite = new Sprite2D
            {
                Texture = texture,
                Position = new Vector2(x * _tileSize + _tileSize / 2, y * _tileSize + _tileSize / 2),
                ZIndex = 10 // Above tiles but below player/enemies
            };

            _parent.AddChild(sprite);
            _biomeOverlaySprites.Add(sprite);
        }

        // Log biome distribution
        ILog.Print("[BIOME OVERLAY] Biome distribution from map data:");
        var totalTiles = mapData.Size.X * mapData.Size.Y;
        foreach (var (biomeId, bcount) in biomeCounts)
        {
            var percentage = (float)bcount / totalTiles * 100;
            ILog.Print($"  {biomeId}: {bcount} tiles ({percentage:F1}%)");
        }
    }

    /// <summary>
    /// Clear all biome overlay sprites.
    /// </summary>
    public void Clear()
    {
        foreach (var sprite in _biomeOverlaySprites)
            sprite?.QueueFree();
        _biomeOverlaySprites.Clear();
    }

    private static ImageTexture CreateColorTexture(Color color, int size)
    {
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        image.Fill(color);
        return ImageTexture.CreateFromImage(image);
    }
}
