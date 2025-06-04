using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Card.Services;
using CardCleaner.Scripts.Features.Worldgen;
using Godot;
using Godot.Collections;

namespace CardCleaner.Scripts.Features.Deckbuilder.Models;

/// <summary>
/// Enhanced 3D world-space screen with semantic WFC-based generation
/// </summary>
public partial class WorldTileMapScreen : Node3D
{
    private List<CardSignature> _abilityDeck = new();
    private CardSignature? _mapSeed;
    private Node2D? _playerAgent;
    private TileMapLayer? _tileMap;
    private Camera2D? _camera2D;
    
    // Semantic WFC system
    private Array<SemanticTile> _semanticTiles;
    private SemanticWfcGenerator _semanticGenerator;

    [Export] public SubViewport? Viewport { get; set; }
    [Export] public MeshInstance3D? ScreenMesh { get; set; }
    [Export] public Vector2I ScreenResolution { get; set; } = new(512, 512);
    
    // Path to semantic tile definitions
    [Export] public string TileDefinitionsPath { get; set; } = "res://Assets/Tilesets/SemanticTiles/";
    
    // Alternative: direct semantic tile assignment
    [Export] public Array<SemanticTile> CustomSemanticTiles { get; set; }

    public override void _Ready()
    {
        SetupViewport();
        SetupScreenMaterial();
        LoadSemanticTiles();
    }

    public void Initialize(CardSignature mapSeed, List<CardSignature> abilities)
    {
        ILog.Print("Initializing WorldTileMapScreen with Semantic WFC");
        _mapSeed = mapSeed;
        _abilityDeck = abilities;

        GenerateMap();
        SpawnPlayer();
    }
    
    private void LoadSemanticTiles()
    {
        // Use custom tiles if provided, otherwise load from directory
        if (CustomSemanticTiles != null && CustomSemanticTiles.Count > 0)
        {
            _semanticTiles = CustomSemanticTiles;
        }
        else if (!string.IsNullOrEmpty(TileDefinitionsPath))
        {
            _semanticTiles = LoadSemanticTilesFromDirectory(TileDefinitionsPath);
        }
        
        if (_semanticTiles == null || _semanticTiles.Count == 0)
        {
            ILog.Error("Failed to load semantic tiles");
            return;
        }
        
        ILog.Print($"Loaded {_semanticTiles.Count} semantic tiles");
    }
    
    private Array<SemanticTile> LoadSemanticTilesFromDirectory(string path)
    {
        var tiles = new Array<SemanticTile>();
        var dir = DirAccess.Open(path);
        
        if (dir == null)
        {
            ILog.Error($"Cannot open directory: {path}");
            return tiles;
        }
        
        dir.ListDirBegin();
        string fileName = dir.GetNext();
        
        while (fileName != "")
        {
            if (fileName.EndsWith(".tres") && !dir.CurrentIsDir())
            {
                var tilePath = $"{path}/{fileName}";
                var tile = GD.Load<SemanticTile>(tilePath);
                
                if (tile != null)
                {
                    tiles.Add(tile);
                }
                else
                {
                    ILog.Warning($"Failed to load semantic tile: {tilePath}");
                }
            }
            fileName = dir.GetNext();
        }
        
        return tiles;
    }

    private void SetupViewport()
    {
        if (Viewport == null)
        {
            ILog.Error("SubViewport not assigned");
            return;
        }

        // Configure viewport for 2D-only rendering
        Viewport.Size = ScreenResolution;
        Viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.WhenParentVisible;
        Viewport.Disable3D = true;

        // Get references to 2D nodes inside the viewport
        _tileMap = Viewport.GetNode<TileMapLayer>("TileMapLayer");
        _playerAgent = Viewport.GetNode<Node2D>("PlayerAgent");
        _camera2D = Viewport.GetNode<Camera2D>("Camera2D");

        // Ensure Camera2D is enabled
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
        material.TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest; // Pixel-perfect for tilemaps
    }

    private void GenerateMap()
    {
        if (_tileMap == null || _mapSeed == null || _semanticTiles == null || _semanticTiles.Count == 0) 
        {
            ILog.Error("Cannot generate map - missing required components");
            return;
        }

        // Build runtime TileSet from semantic tiles
        var godotTileSet = BuildTileSetFromSemanticTiles();
        if (godotTileSet == null)
        {
            ILog.Error("Failed to build runtime TileSet from semantic tiles");
            return;
        }
        
        _tileMap.TileSet = godotTileSet;

        // Determine map size based on card rarity
        var rarity = SignatureCardHelper.DetermineRarity(_mapSeed);
        var mapSize = GetMapSizeForRarity(rarity);

        // Apply signature-based weight modifications to tiles
        ApplySignatureInfluence();

        // Create semantic WFC generator
        var seed = (uint)_mapSeed.GetHashCode();
        _semanticGenerator = new SemanticWfcGenerator(_semanticTiles, mapSize, seed);

        // Generate the map using semantic WFC
        var tileGrid = _semanticGenerator.Generate();
        
        // Apply the generated tiles to the TileMapLayer
        ApplySemanticTilesToMap(tileGrid);

        // Configure camera to frame the tilemap
        ConfigureCamera();
        
        ILog.Print($"Generated {mapSize.X}x{mapSize.Y} map using Semantic WFC");
    }
    private TileSet BuildTileSetFromSemanticTiles()
    {
        var tileSet = new TileSet();

        // Create a single atlas source for now
        var atlasSource = new TileSetAtlasSource();

        // Instead of loading a hardcoded texture that doesn't exist,
        // check if semantic tiles have texture information
        if (_semanticTiles.Count > 0)
        {
            // Try to extract texture from first semantic tile if available
            // For now, we'll create a minimal 1x1 white texture as fallback
            var fallbackTexture = CreateFallbackTexture();
            atlasSource.Texture = fallbackTexture;
        
            ILog.Warning("Using fallback texture for TileSet - semantic tile texture mapping not yet implemented");
        }
        else
        {
            ILog.Error("No semantic tiles available to build TileSet from");
            return null;
        }

        tileSet.AddSource(atlasSource, 0);
        return tileSet;
    }

    private ImageTexture CreateFallbackTexture()
    {
        // Create a minimal 32x32 white texture as fallback
        var image = Image.CreateEmpty(32, 32, false, Image.Format.Rgb8);
        image.Fill(Colors.White);
    
        var texture = new ImageTexture();
        texture.SetImage(image);
    
        return texture;
    }

    
    private void ApplySignatureInfluence()
    {
        if (_mapSeed == null || _abilityDeck == null) return;
    
        // Create combined signature from map seed and ability deck
        var combinedSignature = CalculateCombinedSignature();
    
        // Modify tile weights based on signature compatibility
        foreach (var tile in _semanticTiles)
        {
            // Skip tiles without signatures - they remain at base weight
            if (tile.Signature == null)
            {
                ILog.Warning($"Tile '{tile.TileName}' has no signature, skipping signature influence");
                continue;
            }
        
            var compatibility = CalculateSignatureCompatibility(tile.Signature, combinedSignature);
        
            // Apply signature influence as a multiplier to base weight
            // Compatible signatures get boosted, incompatible ones get reduced
            var signatureMultiplier = Mathf.Lerp(0.1f, 2.0f, compatibility);
            tile.BaseWeight *= signatureMultiplier;
        
            // Ensure minimum weight to prevent tiles from becoming impossible
            tile.BaseWeight = Mathf.Max(tile.BaseWeight, 0.01f);
        }
    
        ILog.Print("Applied signature influence to semantic tile weights");
    }
    
    private CardSignature CalculateCombinedSignature()
    {
        // Start with map seed signature
        var combined = new CardSignature(_mapSeed.Elements);
        
        // Blend in ability deck signatures with decreasing influence
        for (int i = 0; i < _abilityDeck.Count; i++)
        {
            var influence = 1.0f / (i + 2); // Decreasing influence for later cards
            var ability = _abilityDeck[i];
            
            for (int j = 0; j < 8; j++)
            {
                combined[j] = Mathf.Lerp(combined[j], ability[j], influence * 0.3f);
            }
        }
        
        return combined;
    }
    private static float CalculateSignatureCompatibility(CardSignature tileSignature, CardSignature targetSignature)
    {
        // Handle null signatures - tiles without signatures have neutral compatibility
        if (tileSignature == null || targetSignature == null)
        {
            return 0.5f; // Neutral compatibility for tiles without signatures
        }
    
        // Calculate how well the tile signature matches the target
        var distance = tileSignature.DistanceTo(targetSignature);
    
        // Convert distance to compatibility (0 = incompatible, 1 = perfect match)
        // Maximum possible distance in 8D space with values [-1,1] is sqrt(8*4) = sqrt(32)
        var maxDistance = Mathf.Sqrt(32);
        var compatibility = 1.0f - (distance / maxDistance);
    
        return Mathf.Clamp(compatibility, 0f, 1f);
    }
    
    private void ApplySemanticTilesToMap(SemanticTile[,] tileGrid)
    {
        var height = tileGrid.GetLength(0);
        var width = tileGrid.GetLength(1);
    
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var tile = tileGrid[y, x];
            
                if (tile != null)
                {
                    // Use a simple mapping system based on tile index
                    var tileIndex = System.Array.IndexOf(_semanticTiles.ToArray(), tile);
                    var atlasCoords = new Vector2I(tileIndex % 8, tileIndex / 8); // Arrange in 8x8 grid
                
                    _tileMap.SetCell(new Vector2I(x, y), 0, atlasCoords);
                }
                else
                {
                    // Fallback to first tile
                    _tileMap.SetCell(new Vector2I(x, y), 0, Vector2I.Zero);
                }
            }
        }
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

    private void ConfigureCamera()
    {
        if (_camera2D == null || _tileMap == null) return;

        // Get the used rectangle of the tilemap
        var usedRect = _tileMap.GetUsedRect();
        if (usedRect.Size == Vector2I.Zero) return;

        // Calculate the center of the tilemap in world coordinates
        var centerTile = usedRect.GetCenter();
        var worldCenter = _tileMap.MapToLocal(new Vector2I(centerTile.X, centerTile.Y));

        // Get tile size from the TileSet
        if (_tileMap.TileSet.GetSource(0) is not TileSetAtlasSource atlasSource)
        {
            ILog.Error("TileSet must use AtlasSource");
            return;
        }

        var tileSize = atlasSource.TextureRegionSize;
        worldCenter -= new Vector2(tileSize.X / 2f, tileSize.Y / 2f);

        // Position camera at the center
        _camera2D.GlobalPosition = worldCenter;

        // Calculate zoom to fit the entire tilemap
        var mapPixelSize = new Vector2(usedRect.Size.X * tileSize.X, usedRect.Size.Y * tileSize.Y);
        var viewportSize = new Vector2(ScreenResolution.X, ScreenResolution.Y);

        // Calculate zoom to fit with some padding
        var zoomX = viewportSize.X / mapPixelSize.X;
        var zoomY = viewportSize.Y / mapPixelSize.Y;
        var zoom = Mathf.Min(zoomX, zoomY) * 0.9f; // 0.9f for padding

        _camera2D.Zoom = new Vector2(zoom, zoom);
    }

    private void SpawnPlayer()
    {
        if (_playerAgent == null || _tileMap == null) return;

        // Position player at center of map
        var usedRect = _tileMap.GetUsedRect();
        var centerTile = usedRect.GetCenter();
        var worldPos = _tileMap.MapToLocal(new Vector2I(centerTile.X, centerTile.Y));
        _playerAgent.Position = worldPos;
    }
    
    // Debug methods for development
    public void RegenerateMap()
    {
        if (_mapSeed != null)
        {
            GenerateMap();
        }
    }
    
    public void SetTileDefinitionsPath(string newPath)
    {
        TileDefinitionsPath = newPath;
        LoadSemanticTiles();
    }
    
    public System.Collections.Generic.Dictionary<string, float> GetTileWeights()
    {
        if (_semanticTiles == null) return new System.Collections.Generic.Dictionary<string, float>();
        
        return _semanticTiles.ToDictionary(
            t => t.TileName ?? "unnamed", 
            t => t.BaseWeight
        );
    }
}