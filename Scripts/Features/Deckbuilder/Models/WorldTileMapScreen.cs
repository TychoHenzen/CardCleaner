using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Card.Services;
using CardCleaner.Scripts.Features.Worldgen;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Models;

/// <summary>
/// Enhanced 3D world-space screen with WFC-based generation
/// </summary>
public partial class WorldTileMapScreen : Node3D
{
    private List<CardSignature> _abilityDeck = new();
    private CardSignature? _mapSeed;
    private Node2D? _playerAgent;
    private TileMapLayer? _tileMap;
    private Camera2D? _camera2D;
    
    // WFC system
    private Worldgen.WfcTileSet _wfcTileSet;
    private WaveCollapseGenerator _wfcGenerator;

    [Export] public SubViewport? Viewport { get; set; }
    [Export] public MeshInstance3D? ScreenMesh { get; set; }
    [Export] public Vector2I ScreenResolution { get; set; } = new(512, 512);
    
    // Path to WFC tile definitions
    [Export] public string TileDefinitionsPath { get; set; } = "res://data/wfc_tiles/";
    
    // Alternative: direct tile set assignment
    [Export] public Worldgen.WfcTileSet CustomTileSet { get; set; }

    public override void _Ready()
    {
        SetupViewport();
        SetupScreenMaterial();
        LoadWfcTileSet();
    }

    public void Initialize(CardSignature mapSeed, List<CardSignature> abilities)
    {
        ILog.Print("Initializing WorldTileMapScreen with WFC");
        _mapSeed = mapSeed;
        _abilityDeck = abilities;

        GenerateMap();
        SpawnPlayer();
    }
    
    private void LoadWfcTileSet()
    {
        // Use custom tile set if provided, otherwise load from directory
        if (CustomTileSet != null)
        {
            _wfcTileSet = CustomTileSet;
        }
        else if (!string.IsNullOrEmpty(TileDefinitionsPath))
        {
            _wfcTileSet = WfcTileLoader.LoadFromDirectory(TileDefinitionsPath);
        }
        
        if (_wfcTileSet == null || _wfcTileSet.Tiles.Count == 0)
        {
            ILog.Error("Failed to load WFC tile set");
            return;
        }
        
        ILog.Print($"Loaded WFC tile set with {_wfcTileSet.Tiles.Count} tiles");
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
        if (_tileMap == null || _mapSeed == null || _wfcTileSet == null) 
        {
            ILog.Error("Cannot generate map - missing required components");
            return;
        }

        // Build the runtime TileSet from our WFC tile definitions
        var godotTileSet = _wfcTileSet.BuildRuntimeTileSet();
        if (godotTileSet == null)
        {
            ILog.Error("Failed to build runtime TileSet");
            return;
        }
        
        _tileMap.TileSet = godotTileSet;

        // Determine map size based on card rarity
        var rarity = SignatureCardHelper.DetermineRarity(_mapSeed);
        var mapSize = GetMapSizeForRarity(rarity);

        // Apply signature-based weight modifications to tiles
        ApplySignatureInfluence();

        // Create WFC generator
        var seed = (uint)_mapSeed.GetHashCode();
        _wfcGenerator = new WaveCollapseGenerator(_wfcTileSet.Tiles.ToList(), mapSize, seed);

        // Generate the map using WFC
        var tileGrid = _wfcGenerator.Generate();
        
        // Apply the generated tiles to the TileMapLayer
        ApplyTilesToMap(tileGrid);

        // Configure camera to frame the tilemap
        ConfigureCamera();
        
        ILog.Print($"Generated {mapSize.X}x{mapSize.Y} map using WFC");
    }
    
    private void ApplySignatureInfluence()
    {
        if (_mapSeed == null || _abilityDeck == null) return;
        
        // Create combined signature from map seed and ability deck
        var combinedSignature = CalculateCombinedSignature();
        
        // Modify tile weights based on signature compatibility
        foreach (var tile in _wfcTileSet.Tiles)
        {
            var compatibility = CalculateSignatureCompatibility(tile.Signature, combinedSignature);
            
            // Apply signature influence as a multiplier to base weight
            // Compatible signatures get boosted, incompatible ones get reduced
            var signatureMultiplier = Mathf.Lerp(0.1f, 2.0f, compatibility);
            tile.BaseWeight *= signatureMultiplier;
            
            // Ensure minimum weight to prevent tiles from becoming impossible
            tile.BaseWeight = Mathf.Max(tile.BaseWeight, 0.01f);
        }
        
        ILog.Print("Applied signature influence to tile weights");
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
        // Calculate how well the tile signature matches the target
        var distance = tileSignature.DistanceTo(targetSignature);
        
        // Convert distance to compatibility (0 = incompatible, 1 = perfect match)
        // Maximum possible distance in 8D space with values [-1,1] is sqrt(8*4) = sqrt(32)
        var maxDistance = Mathf.Sqrt(32);
        var compatibility = 1.0f - (distance / maxDistance);
        
        return Mathf.Clamp(compatibility, 0f, 1f);
    }
    
    private void ApplyTilesToMap(int[,] tileGrid)
    {
        var height = tileGrid.GetLength(0);
        var width = tileGrid.GetLength(1);
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var tileIndex = tileGrid[y, x];
                
                // Find the corresponding WFC tile
                var wfcTile = _wfcTileSet.Tiles.FirstOrDefault(t => t.AtlasId == tileIndex);
                
                if (wfcTile != null)
                {
                    // Place tile using atlas coordinates
                    _tileMap.SetCell(new Vector2I(x, y), 0, wfcTile.AtlasCoords);
                }
                else
                {
                    // Fallback to first tile if mapping fails
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
        LoadWfcTileSet();
    }
    
    public List<string> GetTileSetValidationIssues()
    {
        return _wfcTileSet?.ValidateTileSet() ?? new List<string> { "No tile set loaded" };
    }
    
    public Dictionary<string, float> GetTileWeights()
    {
        if (_wfcTileSet == null) return new Dictionary<string, float>();
        
        return _wfcTileSet.Tiles.ToDictionary(
            t => t.TileName ?? "unnamed", 
            t => t.BaseWeight
        );
    }
}