using System;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Utilities;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen;

/// <summary>
/// Represents a complete generated map with all layers extracted and ready for rendering
/// </summary>
public readonly struct GeneratedMap
{
    public readonly SemanticTile?[,] TerrainLayer;
    public readonly SemanticTile?[,] StructureLayer;
    public readonly SemanticTile?[,] DecorationLayer;
    public readonly SemanticTile?[,] EffectLayer;
    public readonly Vector2I MapSize;

    public GeneratedMap(SemanticTile?[,] terrain, SemanticTile?[,] structure,
        SemanticTile?[,] decoration, SemanticTile?[,] effect, Vector2I mapSize)
    {
        TerrainLayer = terrain;
        StructureLayer = structure;
        DecorationLayer = decoration;
        EffectLayer = effect;
        MapSize = mapSize;
    }

    /// <summary>
    /// Generate a complete map using SemanticWfc3dGenerator
    /// </summary>
    public static GeneratedMap Generate(SemanticTile[] allTiles, Vector2I mapSize, RandomNumberGenerator rng, GradientInfluenceComponent gradientInfluence)
    {
        
        ILog.Print($"Loaded tiles: {allTiles.Length} terrain tiles");
        foreach(var tile in allTiles) 
        {
            ILog.Print($"  - {tile.TileName}, Layer: {tile.Layer}, BaseWeight: {tile.BaseWeight}");
        }


        var mapSize3D = new Vector3I(mapSize.X, mapSize.Y, 4); // 4 layers

        var wfc3DGenerator = new SemanticWfc3dGenerator(allTiles, mapSize3D, rng,gradientInfluence);
        var result3D = wfc3DGenerator.Generate();

        // Extract all layers
        var terrainGrid = Extract2DLayer(result3D, 0);
        var structureGrid = Extract2DLayer(result3D, 1);
        var decorationGrid = Extract2DLayer(result3D, 2);
        var effectGrid = Extract2DLayer(result3D, 3);

        return new GeneratedMap(terrainGrid, structureGrid, decorationGrid, effectGrid, mapSize);
    }

    private static SemanticTile?[,] Extract2DLayer(SemanticTile[,,] result3D, int layerIndex)
    {
        var width = result3D.GetLength(2);
        var height = result3D.GetLength(1);
        var layer2D = new SemanticTile?[height, width];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                layer2D[y, x] = result3D[layerIndex, y, x];
            }
        }

        return layer2D;
    }

    /// <summary>
    /// Apply generated map to runtime TileMapLayers
    /// </summary>
    public void RenderToTileMapLayers(TileMapLayer? terrainLayer, TileMapLayer? structureLayer,
        TileMapLayer? decorationLayer, TileMapLayer? effectLayer)
    {
        ApplyTilesToLayer(TerrainLayer, terrainLayer);
        ApplyTilesToLayer(StructureLayer, structureLayer);
        ApplyTilesToLayer(DecorationLayer, decorationLayer);
        ApplyTilesToLayer(EffectLayer, effectLayer);
    }

    /// <summary>
    /// Render generated map to preview image
    /// </summary>
    public Image RenderToImage(int tileSize, bool showTerrain = true, bool showStructure = true,
        bool showDecoration = true, bool showEffects = true)
    {
        var imageSize = new Vector2I(MapSize.X * tileSize, MapSize.Y * tileSize);
        var compositeImage = Image.CreateEmpty(imageSize.X, imageSize.Y, false, Image.Format.Rgba8);
        compositeImage.Fill(new Color(0.2f, 0.2f, 0.3f, 1.0f)); // Dark background

        if (showTerrain) RenderLayerToImage(TerrainLayer, compositeImage, tileSize);
        if (showStructure) RenderLayerToImage(StructureLayer, compositeImage, tileSize);
        if (showDecoration) RenderLayerToImage(DecorationLayer, compositeImage, tileSize);
        if (showEffects) RenderLayerToImage(EffectLayer, compositeImage, tileSize);

        return compositeImage;
    }

    private static void ApplyTilesToLayer(SemanticTile?[,] tileGrid, TileMapLayer? layer)
    {
        if (layer == null) return;

        var height = tileGrid.GetLength(0);
        var width = tileGrid.GetLength(1);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var pattern = tileGrid[y, x];
                if (pattern == null) continue;

                var position = new Vector2I(x, y);
                var tilePlacement = pattern.GetTileAt(new Vector2I(0, 0));

                if (tilePlacement != null)
                {
                    layer.SetCell(position, tilePlacement.AnimationFrames[0].X, tilePlacement.AnimationFrames[0].YZ());
                }
            }
        }
    }

    private void RenderLayerToImage(SemanticTile?[,] grid, Image targetImage, int tileSize)
    {
        var height = grid.GetLength(0);
        var width = grid.GetLength(1);
        var processedPositions = new bool[height, width];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (processedPositions[y, x]) continue;

                var pattern = grid[y, x];
                if (pattern?.TileSet == null) continue;

                // Render the entire pattern (existing logic from WorldTileMapScreen)
                for (int py = 0; py < pattern.Size.Y; py++)
                {
                    for (int px = 0; px < pattern.Size.X; px++)
                    {
                        var worldPos = new Vector2I(x + px, y + py);
                        if (worldPos.X >= width || worldPos.Y >= height) continue;

                        var tilePlacement = pattern.GetTileAt(new Vector2I(px, py));
                        if (tilePlacement != null)
                        {
                            var tileImage = ExtractTileImage(pattern.TileSet, tilePlacement, tileSize);
                            if (tileImage != null)
                            {
                                var destPos = new Vector2I(worldPos.X * tileSize, worldPos.Y * tileSize);
                                targetImage.BlitRect(tileImage,
                                    new Rect2I(Vector2I.Zero, new Vector2I(tileSize, tileSize)), destPos);
                            }
                        }

                        processedPositions[worldPos.Y, worldPos.X] = true;
                    }
                }
            }
        }
    }

    private Image? ExtractTileImage(TileSet tileSet, TilePlacement tilePlacement, int tileSize)
    {
        try
        {
            var source = tileSet.GetSource(tilePlacement.AnimationFrames[0].X);
            if (source is not TileSetAtlasSource atlasSource)
            {
                return CreateFallbackTileImage($"Invalid source: {tilePlacement.AnimationFrames[0].X}", Colors.Red,tileSize);
            }

            var texture = atlasSource.Texture;
            if (texture == null)
            {
                return CreateFallbackTileImage($"No texture in source: {tilePlacement.AnimationFrames[0].X}",
                    Colors.Orange, tileSize);
            }

            var sourceImage = texture.GetImage();
            if (sourceImage == null)
            {
                return CreateFallbackTileImage($"Failed to get image from texture", Colors.Yellow, tileSize);
            }

            var region = atlasSource.GetTileTextureRegion(tilePlacement.AnimationFrames[0].YZ());
            if (region.Size.X <= 0 || region.Size.Y <= 0)
            {
                return CreateFallbackTileImage($"Invalid region: {tilePlacement.AnimationFrames[0]}", Colors.Magenta,
                    tileSize);
            }

            var tileImage = sourceImage.GetRegion(region);
            tileImage.Resize(tileSize, tileSize, Image.Interpolation.Nearest);

            return tileImage;
        }
        catch (Exception e)
        {
            ILog.Error($"Failed to extract tile image: {e.Message}");
            return CreateFallbackTileImage($"Exception: {e.Message[..Math.Min(20, e.Message.Length)]}", Colors.Red,
                tileSize);
        }
    }

    private Image CreateFallbackTileImage(string debugText, Color backgroundColor, int tileSize)
    {
        var fallbackImage = Image.CreateEmpty(tileSize, tileSize, false, Image.Format.Rgba8);
        fallbackImage.Fill(backgroundColor);

        // Add a simple border to make it visible
        for (int i = 0; i < tileSize; i++)
        {
            fallbackImage.SetPixel(i, 0, Colors.White);
            fallbackImage.SetPixel(i, tileSize - 1, Colors.White);
            fallbackImage.SetPixel(0, i, Colors.White);
            fallbackImage.SetPixel(tileSize - 1, i, Colors.White);
        }

        ILog.Warning($"Using fallback tile: {debugText}");
        return fallbackImage;
    }
}