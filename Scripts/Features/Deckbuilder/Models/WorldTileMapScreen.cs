using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Utilities;
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

    // Default values as constants/static readonly
    private static readonly Vector2I DefaultPreviewSize = new(15, 10);
    private const bool DefaultSeeded = true;
    private const bool DefaultShowTerrain = true;
    private const bool DefaultShowStructure = true;
    private const bool DefaultShowDecoration = true;
    private const bool DefaultShowEffects = true;
    private static readonly CardSignature DefaultSignature = new();

    // Layer visibility toggles for preview
    [ExportCategory("Preview")] [Export] public Vector2I PreviewSize { get; set; } = DefaultPreviewSize;
    [Export] public bool Seeded { get; set; } = DefaultSeeded;
    [Export] public bool ShowTerrain { get; set; } = DefaultShowTerrain;
    [Export] public bool ShowStructure { get; set; } = DefaultShowStructure;
    [Export] public bool ShowDecoration { get; set; } = DefaultShowDecoration;
    [Export] public bool ShowEffects { get; set; } = DefaultShowEffects;
    [Export] public CardSignature Signature { get; set; } = new();
    private bool _generatePreview;

    public override bool _PropertyCanRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(PreviewSize) => true,
            nameof(Seeded) => true,
            nameof(ShowTerrain) => true,
            nameof(ShowStructure) => true,
            nameof(ShowDecoration) => true,
            nameof(ShowEffects) => true,
            nameof(Signature) => true,
            _ => base._PropertyCanRevert(property)
        };
    }

    public override Variant _PropertyGetRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(PreviewSize) => Variant.From(DefaultPreviewSize),
            nameof(Seeded) => DefaultSeeded,
            nameof(ShowTerrain) => DefaultShowTerrain,
            nameof(ShowStructure) => DefaultShowStructure,
            nameof(ShowDecoration) => DefaultShowDecoration,
            nameof(ShowEffects) => DefaultShowEffects,
            nameof(Signature) => Variant.From(DefaultSignature),
            _ => base._PropertyGetRevert(property)
        };
    }

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

    private GradientInfluenceComponent _gradientInfluence = null!;

    public override void _Ready()
    {
    }

    public void Initialize(CardSignature[] mapSeed, CardSignature[] abilities)
    {
        if (SemanticTiles.Count == 0)
        {
            ILog.Error("No semantic tiles available for world generation");
            return;
        }

        // Calculate map size and setup
        var generationSeed = GenerateSeedFromCard(mapSeed);
        var rng = new RandomNumberGenerator { Seed = generationSeed };
        var rarity = SignatureCardHelper.DetermineRarity(mapSeed);
        var gradient = new CardBasedGradient(mapSeed, rng);
        _gradientInfluence = new GradientInfluenceComponent(gradient);
        var calculatedMapSize = GetMapSizeForRarity(rarity);
        SetupViewport(calculatedMapSize);
        SetupScreenMaterial();

        // Generate map using shared logic
        var allTilesArray = SemanticTiles.ToArray();
        var generatedMap = GeneratedMap.Generate(allTilesArray, calculatedMapSize, rng, _gradientInfluence);

        // Clear and render to TileMapLayers
        ClearAllLayers();
        generatedMap.RenderToTileMapLayers(TerrainLayer, StructureLayer, DecorationLayer, EffectLayer);

        // Handle enemy layer (existing logic)
        if (EnemySpawnData.Count > 0)
        {
            var enemyGrid = GenerateEnemyLayer(generatedMap.TerrainLayer, EnemySpawnData, gradient, calculatedMapSize,
                rng);
            ApplyEnemyLayer(enemyGrid, EnemyLayer);
        }

        ConfigureCamera();
        ILog.Print($"Initialized world map from seed card '{mapSeed}' with {abilities.Length} abilities");
    }

    private void ClearAllLayers()
    {
        TerrainLayer?.Clear();
        StructureLayer?.Clear();
        DecorationLayer?.Clear();
        EffectLayer?.Clear();
    }

    private void SetupViewport(Vector2I mapSize)
    {
        if (Viewport == null) return;

        // Configure viewport for 2D-only rendering
        Viewport.Size = mapSize * TileSize;
        Viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.WhenParentVisible;
        Viewport.Disable3D = true;

        // Get camera reference
        _camera2D = Viewport.GetNode<Camera2D>("Camera2D");
        if (_camera2D != null) _camera2D.Enabled = true;
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
        var viewportSize = Viewport?.Size ?? new Vector2I(1920, 1080);

        var zoomX = viewportSize.X / mapPixelSize.X;
        var zoomY = viewportSize.Y / mapPixelSize.Y;
        var zoom = Mathf.Min(zoomX, zoomY); // 0.9f for padding

        _camera2D.Zoom = new Vector2(zoom, zoom);
    }

    private ulong GenerateSeedFromCard(CardSignature[] mapSeed)
    {
        // Use card name hash as base seed for reproducible generation
        ulong running = 0;
        foreach (var card in mapSeed)
            running ^= SignatureCardHelper.ComputeSeed(card);
        return running;
    }

    private void OnLayerVisibilityChanged(bool _)
    {
        GeneratePreviewInternal(); // Regenerate preview when layer visibility changes
    }

    private void GeneratePreviewInternal()
    {
        if (SemanticTiles.Count == 0)
        {
            PreviewDisplay = CreateEmptyPreviewWithMessage("No SemanticTiles configured");
            return;
        }

        // Generate map using shared logic
        var seed = Seeded ? GenerateSeedFromCard(new[] { Signature }) : Time.Singleton.GetTicksMsec();
        var allTilesArray = SemanticTiles.ToArray();

        var rng = new RandomNumberGenerator { Seed = seed };
        var gradientSignature = Seeded ? Signature : CardSignature.Random(rng);
        var gradient = new CardBasedGradient(new[] { gradientSignature }, rng);
        _gradientInfluence = new GradientInfluenceComponent(gradient);
        var generatedMap = GeneratedMap.Generate(allTilesArray, PreviewSize, rng, _gradientInfluence);

        // Render to preview image
        var previewImage =
            generatedMap.RenderToImage(TileSize, ShowTerrain, ShowStructure, ShowDecoration, ShowEffects);
        PreviewDisplay = ImageTexture.CreateFromImage(previewImage);

        ILog.Print($"Generated preview with signature: {gradientSignature} (seed: {seed})");
    }

    private ImageTexture CreateEmptyPreviewWithMessage(string message)
    {
        var imageSize = new Vector2I(PreviewSize.X * TileSize, PreviewSize.Y * TileSize);
        var emptyImage = Image.CreateEmpty(imageSize.X, imageSize.Y, false, Image.Format.Rgba8);
        emptyImage.Fill(new Color(0.3f, 0.1f, 0.1f, 1.0f)); // Dark red background

        // Add border
        for (var i = 0; i < imageSize.X; i++)
        {
            emptyImage.SetPixel(i, 0, Colors.White);
            emptyImage.SetPixel(i, imageSize.Y - 1, Colors.White);
        }

        for (var i = 0; i < imageSize.Y; i++)
        {
            emptyImage.SetPixel(0, i, Colors.White);
            emptyImage.SetPixel(imageSize.X - 1, i, Colors.White);
        }

        ILog.Warning($"Preview generation failed: {message}");
        return ImageTexture.CreateFromImage(emptyImage);
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

    private EnemySpawnData?[,] GenerateEnemyLayer(SemanticTile?[,] terrainGrid,
        Array<EnemySpawnData> enemies, CardBasedGradient gradient, Vector2I mapSize, RandomNumberGenerator rng)
    {
        var enemyGrid = new EnemySpawnData?[mapSize.Y, mapSize.X];
        var enemyList = enemies.ToList();

        for (var y = 0; y < mapSize.Y; y++)
        for (var x = 0; x < mapSize.X; x++)
        {
            var terrain = terrainGrid[y, x];
            if (terrain == null) continue;

            // Calculate blended signature
            var position = new Vector2I(x, y);
            var baselineSignature = gradient.GetSignatureAt(position, mapSize);
            var terrainSignature = terrain.Signature ?? new CardSignature();
            var randomOffset = GenerateRandomSignatureOffset(rng);

            var blendedSignature = BlendSignatures(baselineSignature, terrainSignature, randomOffset);

            // Find compatible enemies
            var compatibleEnemies = enemyList.Where(e => e.CanSpawnOnTile(terrain, blendedSignature)).ToList();
            if (compatibleEnemies.Count == 0) continue;

            // Select enemy by weight
            var selectedEnemy = SelectEnemyByWeight(compatibleEnemies, terrain, blendedSignature, rng);
            if (selectedEnemy != null) enemyGrid[y, x] = selectedEnemy;
        }

        return enemyGrid;
    }

    private static CardSignature BlendSignatures(CardSignature baseline, CardSignature terrain, CardSignature random)
    {
        var result = new CardSignature();
        for (var i = 0; i < 8; i++)
        {
            // Weighted blend: 40% baseline, 40% terrain, 20% random
            var blended = baseline[i] * 0.4f + terrain[i] * 0.4f + random[i] * 0.2f;
            result[i] = Mathf.Clamp(blended, -1f, 1f);
        }

        return result;
    }

    private static CardSignature GenerateRandomSignatureOffset(RandomNumberGenerator rng)
    {
        var result = new CardSignature();
        for (var i = 0; i < 8; i++) result[i] = rng.RandfRange(-0.3f, 0.3f); // Small random variation

        return result;
    }

    private static EnemySpawnData? SelectEnemyByWeight(List<EnemySpawnData> enemies,
        SemanticTile terrain, CardSignature signature, RandomNumberGenerator rng)
    {
        var totalWeight = enemies.Sum(e => e.CalculateSpawnWeight(terrain, signature));
        if (totalWeight <= 0) return null;

        var randomValue = rng.Randf() * totalWeight;
        var currentWeight = 0f;

        foreach (var enemy in enemies)
        {
            currentWeight += enemy.CalculateSpawnWeight(terrain, signature);
            if (randomValue <= currentWeight)
                return enemy;
        }

        return enemies.LastOrDefault();
    }

    private static void ApplyEnemyLayer(EnemySpawnData?[,] enemyGrid, TileMapLayer? layer)
    {
        if (layer == null) return;

        var height = enemyGrid.GetLength(0);
        var width = enemyGrid.GetLength(1);

        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var enemy = enemyGrid[y, x];
            if (enemy == null) continue;

            var position = new Vector2I(x, y);
            layer.SetCell(position, enemy.SourceId, enemy.AtlasCoords);
        }
    }
}