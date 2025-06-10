using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Enum;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Card.Services;
using CardCleaner.Scripts.Features.Worldgen;
using Godot;
using Godot.Collections;

namespace CardCleaner.Scripts.Features.Deckbuilder.Models;

/// <summary>
/// Enhanced 3D world-space screen with layered world generation
/// </summary>
[Tool]
[GlobalClass]
public partial class WorldTileMapScreen : Node3D
{
    [Export] public WorldData? Configuration { get; set; }
    public Array<SemanticTile> SemanticTiles => Configuration?.SemanticTiles ?? new Array<SemanticTile>();
    public int TileSize => Configuration?.TileSize ?? 32;
    public Array<EnemySpawnData> EnemySpawnData => Configuration?.EnemySpawnData ?? new Array<EnemySpawnData>();

    // Runtime TileMapLayer nodes for actual gameplay
    [Export] public TileMapLayer? TerrainLayer { get; set; }
    [Export] public TileMapLayer? StructureLayer { get; set; }
    [Export] public TileMapLayer? DecorationLayer { get; set; }
    [Export] public TileMapLayer? EffectLayer { get; set; }
    
    [Export] public TileMapLayer? EnemyLayer { get; set; }
    
    [Export] public SubViewport? Viewport { get; set; }
    [Export] public MeshInstance3D? ScreenMesh { get; set; }
    private Camera2D? _camera2D;


    // Layer visibility toggles for preview
    [ExportCategory("Preview")] [Export] public Vector2I PreviewSize { get; set; } = new(15, 10);
    [Export] public bool Seeded { get; set; } = true;
    [Export] public bool ShowTerrain { get; set; } = true;
    [Export] public bool ShowStructure { get; set; } = true;
    [Export] public bool ShowDecoration { get; set; } = true;
    [Export] public bool ShowEffects { get; set; } = true;
    [Export] public CardSignature Signature { get; set; } = new();
    private bool _generatePreview = false;

    [Export]
    public bool GeneratePreview
    {
        get => _generatePreview;
        set
        {
            if (!value) return;
            GeneratePreviewInternal();
            _generatePreview = false;
            NotifyPropertyListChanged();
        }
    }

    [Export] public ImageTexture? PreviewDisplay { get; set; }

    private LayeredWorldGenerator? _worldGenerator;

    public override void _Ready()
    {
        EnsureWorldGeneratorInitialized();
    }

    private void EnsureWorldGeneratorInitialized()
    {
        _worldGenerator ??= new LayeredWorldGenerator(SemanticTiles);
    }

    /// <summary>
    /// Initialize the world map from deckbuilder cards - generates actual gameplay map
    /// </summary>
    public void Initialize(CardSignature[] mapSeed, CardSignature[] abilities)
    {
        if (SemanticTiles.Count == 0)
        {
            ILog.Error("No semantic tiles available for world generation");
            return;
        }

        if (ILog.ExportCheck(Viewport, nameof(Viewport), this))
            return;
        
        // Calculate map size based on card rarity
        var rarity = SignatureCardHelper.DetermineRarity(mapSeed);
        var calculatedMapSize = GetMapSizeForRarity(rarity);

        SetupViewport(calculatedMapSize);
        SetupScreenMaterial();

        // Convert card signature to generation seed
        ulong generationSeed = GenerateSeedFromCard(mapSeed);

        if (ILog.ExportCheck(TerrainLayer, nameof(TerrainLayer), this) ||
            ILog.ExportCheck(StructureLayer, nameof(StructureLayer), this) ||
            ILog.ExportCheck(DecorationLayer, nameof(DecorationLayer), this) ||
            ILog.ExportCheck(EffectLayer, nameof(EffectLayer), this) ||
            ILog.ExportCheck(EnemyLayer, nameof(EnemyLayer), this))
            return;
        
        TerrainLayer!.Clear();
        StructureLayer!.Clear();
        DecorationLayer!.Clear();
        EffectLayer!.Clear();
        // Generate using the existing layer system
        EnsureWorldGeneratorInitialized();
        _worldGenerator!.Generate(generationSeed, TerrainLayer, StructureLayer, 
            DecorationLayer, EffectLayer, EnemyLayer, calculatedMapSize, 
            new CardBasedGradient(mapSeed), EnemySpawnData);

        // Configure camera to frame the tilemap
        ConfigureCamera();

        ILog.Print($"Initialized world map from seed card '{mapSeed}' with {abilities.Length} abilities");
    }

private void SetupViewport(Vector2I mapSize)
{
    if (Viewport == null) return;

    // Configure viewport for 2D-only rendering
    Viewport.Size = mapSize*TileSize;
    Viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.WhenParentVisible;
    Viewport.Disable3D = true;

    // Get camera reference
    _camera2D = Viewport.GetNode<Camera2D>("Camera2D");
    if (_camera2D != null)
    {
        _camera2D.Enabled = true;
    }
}

private void SetupScreenMaterial()
{
    if (Viewport == null || ScreenMesh?.MaterialOverride is not StandardMaterial3D material) 
        return;
        
    // Use viewport texture as albedo
    material.AlbedoTexture = Viewport.GetTexture();
    material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
    material.DisableReceiveShadows = true;
    material.TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest;
}

private void ConfigureCamera()
{
    if (_camera2D == null) return;

    if (TerrainLayer == null) return;

    // Get the used rectangle and center camera
    var usedRect = TerrainLayer.GetUsedRect();
    if (usedRect.Size == Vector2I.Zero) return;

    var centerTile = usedRect.GetCenter();
    var worldCenter = TerrainLayer.MapToLocal(new Vector2I(centerTile.X, centerTile.Y));
    if (TerrainLayer.TileSet.GetSource(0) is not TileSetAtlasSource atlasSource)
    {
        ILog.Error("TileSet must use AtlasSource");
        return;
    }
    var tileSize = atlasSource.TextureRegionSize;
    worldCenter -= new Vector2(tileSize.X / 2f, tileSize.Y / 2f);
    
    _camera2D.GlobalPosition = worldCenter;
    
    // Calculate zoom to fit the entire tilemap
    var mapPixelSize = new Vector2(usedRect.Size.X * TileSize, usedRect.Size.Y * TileSize); // Assuming 32px tiles
    var viewportSize = Viewport.Size;
    
    var zoomX = viewportSize.X / mapPixelSize.X;
    var zoomY = viewportSize.Y / mapPixelSize.Y;
    var zoom = Mathf.Min(zoomX, zoomY); // 0.9f for padding
    
    _camera2D.Zoom = new Vector2(zoom, zoom);
}

    private ulong GenerateSeedFromCard(CardSignature[] mapSeed)
    {
        // Use card name hash as base seed for reproducible generation
        long running = 0;
        foreach (var card in mapSeed)
            running ^= card.GetHashCode();
        return (ulong)running;
    }

    private void OnLayerVisibilityChanged(bool _)
    {
        GeneratePreviewInternal(); // Regenerate preview when layer visibility changes
    }

    private void GeneratePreviewInternal()
    {
        if (SemanticTiles.Count == 0)
        {
            ILog.Error("No semantic tiles available for preview generation");
            PreviewDisplay = CreateEmptyPreviewWithMessage("No SemanticTiles configured");
            return;
        }

        // Validate that tiles have proper TileSet references
        int validTileCount = 0;
        foreach (var tile in SemanticTiles)
        {
            if (tile.TileSet != null && tile.Tile != null)
                validTileCount++;
        }
    
        if (validTileCount == 0)
        {
            ILog.Error("No semantic tiles have valid TileSet references");
            PreviewDisplay = CreateEmptyPreviewWithMessage($"SemanticTiles missing TileSet data ({SemanticTiles.Count} tiles found)");
            return;
        }

        ILog.Print($"Found {validTileCount}/{SemanticTiles.Count} valid semantic tiles");


        EnsureWorldGeneratorInitialized();

        // Convert CardSignature to seed
        var seed = Seeded ? GenerateSeedFromCard(new []{Signature}) : Time.Singleton.GetTicksMsec();

        // Generate logical tile grids using the actual generation algorithm
        var tileGrids = GenerateTileGrids(seed);

        // Composite the grids into a preview image
        var previewImage = CompositeTileGridsToImage(tileGrids);

        // Display the preview
        PreviewDisplay = ImageTexture.CreateFromImage(previewImage);

        ILog.Print($"Generated preview with signature: {Signature} (seed: {seed})");
    }
    
    private ImageTexture CreateEmptyPreviewWithMessage(string message)
    {
        var imageSize = new Vector2I(PreviewSize.X * TileSize, PreviewSize.Y * TileSize);
        var emptyImage = Image.CreateEmpty(imageSize.X, imageSize.Y, false, Image.Format.Rgba8);
        emptyImage.Fill(new Color(0.3f, 0.1f, 0.1f, 1.0f)); // Dark red background
    
        // Add border
        for (int i = 0; i < imageSize.X; i++)
        {
            emptyImage.SetPixel(i, 0, Colors.White);
            emptyImage.SetPixel(i, imageSize.Y - 1, Colors.White);
        }
        for (int i = 0; i < imageSize.Y; i++)
        {
            emptyImage.SetPixel(0, i, Colors.White);
            emptyImage.SetPixel(imageSize.X - 1, i, Colors.White);
        }
    
        ILog.Warning($"Preview generation failed: {message}");
        return ImageTexture.CreateFromImage(emptyImage);
    }


    private uint GenerateSeedFromCardSignature(CardSignature signature)
    {
        return (uint)signature.GetHashCode();
    }

    private System.Collections.Generic.Dictionary<TileLayer, SemanticTile?[,]> GenerateTileGrids(ulong seed)
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

    private Image? CompositeTileGridsToImage(
        System.Collections.Generic.Dictionary<TileLayer, SemanticTile?[,]> tileGrids)
    {
        var imageSize = new Vector2I(PreviewSize.X * TileSize, PreviewSize.Y * TileSize);
        var compositeImage = Image.CreateEmpty(imageSize.X, imageSize.Y, false, Image.Format.Rgba8);
        compositeImage.Fill(new Color(0.2f, 0.2f, 0.3f, 1.0f)); // Dark background

        // Define layer rendering order
        var layerOrder = new[] { TileLayer.Terrain, TileLayer.Structure, TileLayer.Decoration, TileLayer.Effects };
        var layerVisibility = new System.Collections.Generic.Dictionary<TileLayer, bool>
        {
            [TileLayer.Terrain] = ShowTerrain,
            [TileLayer.Structure] = ShowStructure,
            [TileLayer.Decoration] = ShowDecoration,
            [TileLayer.Effects] = ShowEffects
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
        try
        {
            var source = tileSet.GetSource(tilePlacement.AnimationFrames[0].X);
            if (source is not TileSetAtlasSource atlasSource)
            {
                return CreateFallbackTileImage($"Invalid source: {tilePlacement.AnimationFrames[0].X}", Colors.Red);
            }

            var texture = atlasSource.Texture;
            if (texture == null)
            {
                return CreateFallbackTileImage($"No texture in source: {tilePlacement.AnimationFrames[0].X}", Colors.Orange);
            }

            var sourceImage = texture.GetImage();
            if (sourceImage == null)
            {
                return CreateFallbackTileImage($"Failed to get image from texture", Colors.Yellow);
            }

            var region = atlasSource.GetTileTextureRegion(tilePlacement.AnimationFrames[0].YZ());
            if (region.Size.X <= 0 || region.Size.Y <= 0)
            {
                return CreateFallbackTileImage($"Invalid region: {tilePlacement.AnimationFrames[0]}", Colors.Magenta);
            }

            var tileImage = sourceImage.GetRegion(region);
            tileImage.Resize(TileSize, TileSize, Image.Interpolation.Nearest);

            return tileImage;
        }
        catch (Exception e)
        {
            ILog.Error($"Failed to extract tile image: {e.Message}");
            return CreateFallbackTileImage($"Exception: {e.Message[..Math.Min(20, e.Message.Length)]}", Colors.Red);
        }
    }

    private Image CreateFallbackTileImage(string debugText, Color backgroundColor)
    {
        var fallbackImage = Image.CreateEmpty(TileSize, TileSize, false, Image.Format.Rgba8);
        fallbackImage.Fill(backgroundColor);

        // Add a simple border to make it visible
        for (int i = 0; i < TileSize; i++)
        {
            fallbackImage.SetPixel(i, 0, Colors.White);
            fallbackImage.SetPixel(i, TileSize - 1, Colors.White);
            fallbackImage.SetPixel(0, i, Colors.White);
            fallbackImage.SetPixel(TileSize - 1, i, Colors.White);
        }

        ILog.Warning($"Using fallback tile: {debugText}");
        return fallbackImage;
    }
    private static Vector2I GetMapSizeForRarity(CardRarity rarity)
    {
        var height = rarity switch
        {
            CardRarity.Common => 16,
            CardRarity.Uncommon => 32,
            CardRarity.Rare => 64,
            CardRarity.Epic => 128,
            CardRarity.Legendary => 256,
            _ => 16 // fallback to common size
        };

        // Calculate width using 16:9 aspect ratio
        var width = Mathf.RoundToInt(height * 16f / 9f);

        return new Vector2I(width, height);
    }

}