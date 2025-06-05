using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Enum;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Worldgen;
using Godot;
using Godot.Collections;

namespace CardCleaner.Scripts.Features.Deckbuilder.Models;

/// <summary>
/// Enhanced 3D world-space screen with layered world generation
/// </summary>
public partial class WorldTileMapScreen : Control
{
    [Export] public Array<SemanticTile> SemanticTiles { get; set; } = new();
    [Export] public Button? GeneratePreviewButton { get; set; }
    [Export] public SpinBox? SeedInput { get; set; }
    [Export] public TextureRect? PreviewDisplay { get; set; }
    [Export] public Vector2I PreviewSize { get; set; } = new(15, 10);
    [Export] public Vector2I RuntimeMapSize { get; set; } = new(100, 100);
    [Export] public int TileSize { get; set; } = 32;
    
    // Runtime TileMapLayer nodes for actual gameplay
    [Export] public TileMapLayer? TerrainLayer { get; set; }
    [Export] public TileMapLayer? StructureLayer { get; set; }
    [Export] public TileMapLayer? DecorationLayer { get; set; }
    [Export] public TileMapLayer? EffectLayer { get; set; }
    
    // Layer visibility toggles for preview
    [Export] public CheckBox? ShowTerrain { get; set; }
    [Export] public CheckBox? ShowStructure { get; set; }
    [Export] public CheckBox? ShowDecoration { get; set; }
    [Export] public CheckBox? ShowEffects { get; set; }

    private LayeredWorldGenerator _worldGenerator;
    
    public override void _Ready()
    {
        _worldGenerator = new LayeredWorldGenerator(SemanticTiles);
        GeneratePreviewButton?.Connect(Button.SignalName.Pressed, new Callable(this, nameof(GeneratePreview)));
        ShowTerrain?.Connect(CheckBox.SignalName.Toggled, new Callable(this, nameof(OnLayerVisibilityChanged)));
        ShowStructure?.Connect(CheckBox.SignalName.Toggled, new Callable(this, nameof(OnLayerVisibilityChanged)));
        ShowDecoration?.Connect(CheckBox.SignalName.Toggled, new Callable(this, nameof(OnLayerVisibilityChanged)));
        ShowEffects?.Connect(CheckBox.SignalName.Toggled, new Callable(this, nameof(OnLayerVisibilityChanged)));
        
        // Set default visibility
        if (ShowTerrain != null) ShowTerrain.ButtonPressed = true;
        if (ShowStructure != null) ShowStructure.ButtonPressed = true;
        if (ShowDecoration != null) ShowDecoration.ButtonPressed = true;
        if (ShowEffects != null) ShowEffects.ButtonPressed = true;
        
        GeneratePreview();
    }

    /// <summary>
    /// Initialize the world map from deckbuilder cards - generates actual gameplay map
    /// </summary>
    public void Initialize(CardSignature mapSeed, CardSignature[] abilities)
    {
        if (SemanticTiles.Count == 0)
        {
            ILog.Error("No semantic tiles available for world generation");
            return;
        }

        // Convert card signature to generation seed
        uint generationSeed = GenerateSeedFromCard(mapSeed);
        
        // Apply abilities to modify generation parameters if needed
        ModifyGenerationFromAbilities(abilities);
        
        // Generate the actual runtime map
        _worldGenerator.Generate(generationSeed, TerrainLayer, StructureLayer, 
            DecorationLayer, EffectLayer, RuntimeMapSize);
        
        ILog.Print($"Initialized world map from seed card '{mapSeed}' with {abilities.Length} abilities");
    }
    
    private uint GenerateSeedFromCard(CardSignature mapSeed)
    {
        // Use card name hash as base seed for reproducible generation
        return (uint)mapSeed.GetHashCode();
    }
    
    private void ModifyGenerationFromAbilities(CardSignature[] abilities)
    {
        // TODO: Apply ability effects to modify tile weights, available tiles, etc.
        // For now, just log the abilities being applied
        foreach (var ability in abilities)
        {
            ILog.Print($"Applying ability: {ability}");
        }
    }
    
    private void OnLayerVisibilityChanged(bool _)
    {
        GeneratePreview(); // Regenerate preview when layer visibility changes
    }
    
    private void GeneratePreview()
    {
        if (SemanticTiles.Count == 0)
        {
            GD.PrintErr("No semantic tiles available for preview generation");
            return;
        }
        
        var seed = (uint)(SeedInput?.Value ?? GD.Randi());
        
        // Generate logical tile grids using the actual generation algorithm
        var tileGrids = GenerateTileGrids(seed);
        
        // Composite the grids into a preview image
        var previewImage = CompositeTileGridsToImage(tileGrids);
        
        // Display the preview
        if (PreviewDisplay != null && previewImage != null)
        {
            PreviewDisplay.Texture = ImageTexture.CreateFromImage(previewImage);
        }
        
        GD.Print($"Generated preview with seed: {seed}");
    }
    
    private System.Collections.Generic.Dictionary<TileLayer, SemanticTile?[,]> GenerateTileGrids(uint seed)
    {
        var rng = new RandomNumberGenerator { Seed = seed };
        var grids = new System.Collections.Generic.Dictionary<TileLayer, SemanticTile?[,]>();
        
        // Cache tiles by layer
        var terrainTiles = SemanticTiles.Where(t => t.Layer == TileLayer.Terrain).ToList();
        var structureTiles = SemanticTiles.Where(t => t.Layer == TileLayer.Structure).ToList();
        var decorationTiles = SemanticTiles.Where(t => t.Layer == TileLayer.Decoration).ToList();
        var effectTiles = SemanticTiles.Where(t => t.Layer == TileLayer.Effects).ToList();
        
        // Generate terrain layer using WFC
        SemanticTile?[,] terrainGrid = null;
        if (terrainTiles.Count > 0)
        {
            var terrainArray = new Array<SemanticTile>();
            foreach (var tile in terrainTiles) terrainArray.Add(tile);
            
            var wfcGenerator = new SemanticWfcGenerator(terrainArray, PreviewSize, seed);
            terrainGrid = wfcGenerator.Generate();
            grids[TileLayer.Terrain] = terrainGrid;
        }
        
        // Generate subsequent layers on top of terrain
        if (terrainGrid != null)
        {
            var structureGrid = _worldGenerator.GenerateLayerOnTop(terrainGrid, structureTiles, PreviewSize, rng);
            grids[TileLayer.Structure] = structureGrid;
            
            var decorationGrid = _worldGenerator.GenerateLayerOnTop(structureGrid, decorationTiles, PreviewSize, rng);
            grids[TileLayer.Decoration] = decorationGrid;
            
            var effectGrid = _worldGenerator.GenerateLayerOnTop(decorationGrid, effectTiles, PreviewSize, rng);
            grids[TileLayer.Effects] = effectGrid;
        }
        
        return grids;
    }
    
    private Image? CompositeTileGridsToImage(System.Collections.Generic.Dictionary<TileLayer, SemanticTile?[,]> tileGrids)
    {
        var imageSize = new Vector2I(PreviewSize.X * TileSize, PreviewSize.Y * TileSize);
        var compositeImage = Image.CreateEmpty(imageSize.X, imageSize.Y, false, Image.Format.Rgba8);
        compositeImage.Fill(new Color(0.2f, 0.2f, 0.3f, 1.0f)); // Dark background
        
        // Define layer rendering order
        var layerOrder = new[] { TileLayer.Terrain, TileLayer.Structure, TileLayer.Decoration, TileLayer.Effects };
        var layerVisibility = new System.Collections.Generic.Dictionary<TileLayer, bool>
        {
            [TileLayer.Terrain] = ShowTerrain?.ButtonPressed ?? true,
            [TileLayer.Structure] = ShowStructure?.ButtonPressed ?? true,
            [TileLayer.Decoration] = ShowDecoration?.ButtonPressed ?? true,
            [TileLayer.Effects] = ShowEffects?.ButtonPressed ?? true
        };
        
        // Render each layer in order
        foreach (var layer in layerOrder)
        {
            if (!layerVisibility[layer] || !tileGrids.ContainsKey(layer)) continue;
            
            var grid = tileGrids[layer];
            RenderLayerToImage(grid, compositeImage);
        }
        
        return compositeImage;
    }
    
    private void RenderLayerToImage(SemanticTile?[,] grid, Image targetImage)
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
                
                // Render the entire pattern
                for (int py = 0; py < pattern.Size.Y; py++)
                {
                    for (int px = 0; px < pattern.Size.X; px++)
                    {
                        var worldPos = new Vector2I(x + px, y + py);
                        if (worldPos.X >= width || worldPos.Y >= height) continue;
                        
                        var tilePlacement = pattern.GetTileAt(new Vector2I(px, py));
                        if (tilePlacement != null)
                        {
                            var tileImage = ExtractTileImage(pattern.TileSet, tilePlacement);
                            if (tileImage != null)
                            {
                                var destPos = new Vector2I(worldPos.X * TileSize, worldPos.Y * TileSize);
                                targetImage.BlitRect(tileImage, 
                                    new Rect2I(Vector2I.Zero, new Vector2I(TileSize, TileSize)),
                                    destPos);
                            }
                        }
                        
                        processedPositions[worldPos.Y, worldPos.X] = true;
                    }
                }
            }
        }
    }
    
    private Image? ExtractTileImage(TileSet tileSet, TilePlacement tilePlacement)
    {
        var source = tileSet.GetSource(tilePlacement.SourceId);
        if (source is not TileSetAtlasSource atlasSource) return null;
        
        var texture = atlasSource.Texture;
        if (texture == null) return null;
        
        var sourceImage = texture.GetImage();
        if (sourceImage == null) return null;
        
        var region = atlasSource.GetTileTextureRegion(tilePlacement.AtlasCoords);
        
        var tileImage = sourceImage.GetRegion(region);
        tileImage.Resize(TileSize, TileSize, Image.Interpolation.Nearest);
        
        return tileImage;
    }
}