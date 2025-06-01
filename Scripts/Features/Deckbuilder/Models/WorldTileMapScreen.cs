using System.Collections.Generic;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Models;

/// <summary>
/// 3D world-space screen that displays a 2D tilemap through a SubViewport
/// </summary>
public partial class WorldTileMapScreen : Node3D
{
    private List<CardSignature> _abilityDeck;
    private CardSignature _mapSeed;
    private Node2D _playerAgent;
    private TileMapLayer _tileMap;
    
    [Export] public SubViewport Viewport { get; set; }
    [Export] public MeshInstance3D ScreenMesh { get; set; }
    [Export] public Vector2I ScreenResolution { get; set; } = new(512, 512);
    
    public override void _Ready()
    {
        SetupViewport();
        SetupScreenMaterial();
    }
    
    public void Initialize(CardSignature mapSeed, List<CardSignature> abilities)
    {
        _mapSeed = mapSeed;
        _abilityDeck = abilities;
        
        GenerateMap();
        SpawnPlayer();
    }
    
    private void SetupViewport()
    {
        if (Viewport == null)
        {
            GD.PrintErr("[WorldTileMapScreen] SubViewport not assigned");
            return;
        }
        
        // Configure viewport for 2D-only rendering
        Viewport.Size = ScreenResolution;
        Viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.WhenParentVisible;
        Viewport.Disable3D = true;
        
        // Get references to 2D nodes inside the viewport
        _tileMap = Viewport.GetNode<TileMapLayer>("TileMapLayer");
        _playerAgent = Viewport.GetNode<Node2D>("PlayerAgent");
    }
    
    private void SetupScreenMaterial()
    {
        if (ScreenMesh?.MaterialOverride is StandardMaterial3D material)
        {
            // Use viewport texture as albedo
            material.AlbedoTexture = Viewport.GetTexture();
            material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            material.DisableReceiveShadows = true;
            material.TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest; // Pixel-perfect for tilemaps
        }
        else
        {
            GD.PrintErr("[WorldTileMapScreen] ScreenMesh needs StandardMaterial3D");
        }
    }
    
    private void GenerateMap()
    {
        if (_tileMap == null) return;
        
        // Simple map generation based on map seed
        var rng = new RandomNumberGenerator();
        rng.Seed = (uint)_mapSeed.GetHashCode();
        
        // Generate a 16x16 map with some variety
        for (var x = 0; x < 16; x++)
        {
            for (var y = 0; y < 16; y++)
            {
                var tileId = rng.RandiRange(0, 2); // Assume tileset has 3 tiles
                _tileMap.SetCell(new Vector2I(x, y), 0, new Vector2I(tileId, 0));
            }
        }
    }
    
    private void SpawnPlayer()
    {
        if (_playerAgent == null) return;
        
        // Position player at center of map
        var centerTile = new Vector2I(8, 8);
        var worldPos = _tileMap.MapToLocal(centerTile);
        _playerAgent.Position = worldPos;
    }
}