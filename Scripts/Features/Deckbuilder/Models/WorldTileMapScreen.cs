using System.Collections.Generic;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Card.Services;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Models;

/// <summary>
/// 3D world-space screen that displays a 2D tilemap through a SubViewport
/// </summary>
public partial class WorldTileMapScreen : Node3D
{
    private List<CardSignature> _abilityDeck = new();
    private CardSignature? _mapSeed;
    private Node2D? _playerAgent;
    private TileMapLayer? _tileMap;
    private Camera2D? _camera2D;

    [Export] public SubViewport? Viewport { get; set; }
    [Export] public MeshInstance3D? ScreenMesh { get; set; }
    [Export] public Vector2I ScreenResolution { get; set; } = new(512, 512);

    public override void _Ready()
    {
        SetupViewport();
        SetupScreenMaterial();
    }

    public void Initialize(CardSignature mapSeed, List<CardSignature> abilities)
    {
        GD.Print("Initializing WorldTileMapScreen");
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
        _camera2D = Viewport.GetNode<Camera2D>("Camera2D");

        // Ensure Camera2D is enabled
        if (_camera2D != null)
        {
            _camera2D.Enabled = true;
        }

        // Ensure TileMapLayer has a valid TileSet
        if (_tileMap is { TileSet: null })
        {
            var tileSet = new TileSet();
            var source = new TileSetAtlasSource();
            source.Texture = GD.Load<Texture2D>("res://icon.svg");
            source.TextureRegionSize = new Vector2I(128, 128);

            for (int i = 0; i < 3; i++)
            {
                source.CreateTile(new Vector2I(i, 0));
            }

            tileSet.AddSource(source, 0);
            _tileMap.TileSet = tileSet;

            GD.Print("[WorldTileMapScreen] Created basic TileSet for TileMapLayer");
        }
    }

    private void SetupScreenMaterial()
    {
        if (Viewport == null)
            return;
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
        if (_tileMap == null || _mapSeed == null) return;

        // Determine map size based on card rarity
        var rarity = SignatureCardHelper.DetermineRarity(_mapSeed);
        var mapSize = GetMapSizeForRarity(rarity);

        // Simple map generation based on map seed
        var rng = new RandomNumberGenerator();
        rng.Seed = (uint)_mapSeed.GetHashCode();

        // Generate a map of variable size with some variety
        for (var x = 0; x < mapSize.X; x++)
        {
            for (var y = 0; y < mapSize.Y; y++)
            {
                var tileId = rng.RandiRange(0, 2);
                _tileMap.SetCell(new Vector2I(x, y), 0, new Vector2I(tileId, 0));
            }
        }


        // Configure camera to frame the tilemap
        ConfigureCamera();
    }

    private Vector2I GetMapSizeForRarity(CardRarity rarity)
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
    
        // Calculate width using 16:9 aspect ratio (1920:1080)
        var width = Mathf.RoundToInt(height * 16f / 9f);
    
        return new Vector2I(width, height);
    }


    private void ConfigureCamera()
    {
        if (_camera2D == null || _tileMap == null) return;

        // Get the used rectangle of the tilemap
        var usedRect = _tileMap.GetUsedRect();

        // Calculate the center of the tilemap in world coordinates
        var centerTile = usedRect.GetCenter();
        var worldCenter = _tileMap.MapToLocal(new Vector2I(centerTile.X, centerTile.Y));

        // Calculate zoom to fit the entire tilemap in the 
        if (_tileMap.TileSet.GetSource(0) is not TileSetAtlasSource atlasTileset)
        {
            GD.Print("[WorldTileMapScreen] TileSet must use AtlasSource");
            return;
        }

        var tileSize = atlasTileset.TextureRegionSize;
        worldCenter -= new Vector2(tileSize.X / 2f, tileSize.Y / 2f);

        // Position camera at the center
        _camera2D.GlobalPosition = worldCenter;

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
        var centerTile = new Vector2I(8, 8);
        var worldPos = _tileMap.MapToLocal(centerTile);
        _playerAgent.Position = worldPos;
    }
}