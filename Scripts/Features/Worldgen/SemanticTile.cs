using System;
using System.Linq;
using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Core.Enum;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen;

[Tool]
[GlobalClass]
public partial class SemanticTile : Resource
{
    [Export] public string TileName { get; set; } = "";
    [Export] public TilePassability Passability { get; set; } = TilePassability.Passable;

    // Use SpriteRegion system like WfcTile
    [Export] public TileSet? TileSet { get; set; }
    [Export] public SpriteRegion? SpriteRegion { get; set; } // Single region instead of array
    [Export] public float FrameDuration { get; set; } = 0.0f;

    // Tile pattern spawning 
    [Export] public float GlobalSpawnChance { get; set; } = 0.0f; // Chance ANY pattern spawns here
    [Export] public TilePattern[] SpawnPatterns { get; set; } = Array.Empty<TilePattern>();

    // WFC constraint sockets
    [Export] public SocketType North { get; set; } = SocketType.Any;
    [Export] public SocketType East { get; set; } = SocketType.Any;
    [Export] public SocketType South { get; set; } = SocketType.Any;
    [Export] public SocketType West { get; set; } = SocketType.Any;

    private bool _updatePreview;

    [Export]
    public bool UpdatePreview
    {
        get => _updatePreview;
        set
        {
            if (!value) return;
            UpdatePreviewInternal();
            _updatePreview = false;
            NotifyPropertyListChanged();
        }
    }

    [Export] public ImageTexture? PreviewTexture { get; private set; }

    public SocketType[] Sockets
    {
        get { return new[] { North, East, South, West }; }
        set
        {
            North = value[0];
            East = value[1];
            South = value[2];
            West = value[3];
        }
    }

    [Export] public CardSignature Signature { get; set; }
    [Export] public float BaseWeight { get; set; } = 1.0f;

    // Animation support like WfcTile
    public bool IsAnimated => FrameDuration > 0.0f && SpriteRegion?.Layers?.Length > 1;

    // Runtime properties for tileset integration
    public int AtlasId { get; set; } = -1;
    public Vector2I AtlasCoords { get; set; }
    public Vector2I[] AnimationFrames { get; set; } = Array.Empty<Vector2I>();

    private void UpdatePreviewInternal()
    {
        const int gridSize = 8;
        const int baseTileSize = 16;
        var previewSize = new Vector2I(gridSize * 3 * baseTileSize, gridSize * baseTileSize);
        var gridDimensions = new Vector2I(gridSize * 3, gridSize);

        var previewImage = Image.CreateEmpty(previewSize.X, previewSize.Y, false, Image.Format.Rgba8);
        previewImage.Fill(new Color(0.1f, 0.1f, 0.1f, 1.0f));

        // Get base tile graphics
        var baseTileImage = GetBaseTileImage();
        if (baseTileImage == null)
        {
            PreviewTexture = null;
            return;
        }

        baseTileImage.Resize(baseTileSize, baseTileSize, Image.Interpolation.Nearest);

        // Fill grid with base tile
        for (int gridY = 0; gridY < gridDimensions.Y; gridY++)
        {
            for (int gridX = 0; gridX < gridDimensions.X; gridX++)
            {
                var pixelPos = new Vector2I(gridX * baseTileSize, gridY * baseTileSize);
                previewImage.BlitRect(baseTileImage,
                    new Rect2I(Vector2I.Zero, new Vector2I(baseTileSize, baseTileSize)),
                    pixelPos);
            }
        }

        // Apply spawn patterns using new weight-based system
        if (SpawnPatterns != null && SpawnPatterns.Length > 0 && GlobalSpawnChance > 0)
        {
            var random = new Random(); // we do not want a deterministic preview
            var occupiedTiles = new bool[gridDimensions.X, gridDimensions.Y];

            // Position-based iteration (matches runtime behavior)
            for (int gridY = gridDimensions.Y-1; gridY > 0; gridY--)
            {
                for (int gridX = 0; gridX < gridDimensions.X; gridX++)
                {
                    if (occupiedTiles[gridX, gridY]) continue;

                    // Phase 1: Check if ANY pattern should spawn here
                    if (random.NextSingle() > GlobalSpawnChance) continue;

                    // Phase 2: Weight-select which pattern to spawn
                    var selectedPattern = SelectPatternByWeight(SpawnPatterns, random);
                    if (selectedPattern == null) continue;

                    // Check if pattern can fit
                    if (CanPlacePatternInPreview(new Vector2I(gridX, gridY), selectedPattern, gridDimensions,
                            occupiedTiles))
                    {
                        RenderSpawnPattern(previewImage, selectedPattern, new Vector2I(gridX, gridY), baseTileSize);
                        MarkTilesOccupiedInPreview(new Vector2I(gridX, gridY), selectedPattern, occupiedTiles);
                    }
                }
            }
        }

        PreviewTexture = ImageTexture.CreateFromImage(previewImage);
    }

    private TilePattern? SelectPatternByWeight(TilePattern[] patterns, Random random)
    {
        if (patterns.Length == 0) return null;

        float totalWeight = patterns.Sum(pattern => pattern.Weight);

        if (totalWeight <= 0) return patterns[0]; // Fallback to first pattern

        float randomValue = random.NextSingle() * totalWeight;
        float currentWeight = 0;

        foreach (var pattern in patterns)
        {
            currentWeight += pattern.Weight;
            if (randomValue <= currentWeight)
                return pattern;
        }

        return patterns[^1]; // Fallback to last pattern
    }

    private bool CanPlacePatternInPreview(Vector2I position, TilePattern pattern, Vector2I gridDimensions,
        bool[,] occupiedTiles)
    {
        for (int py = 0; py < pattern.Size.Y; py++)
        {
            for (int px = 0; px < pattern.Size.X; px++)
            {
                var checkPos = position + new Vector2I(px, py);

                // Check bounds
                if (checkPos.X >= gridDimensions.X || checkPos.Y >= gridDimensions.Y)
                    return false;

                // Check if tile is already occupied
                if (occupiedTiles[checkPos.X, checkPos.Y])
                    return false;
            }
        }

        return true;
    }

    private void MarkTilesOccupiedInPreview(Vector2I position, TilePattern pattern, bool[,] occupiedTiles)
    {
        for (int py = 0; py < pattern.Size.Y; py++)
        {
            for (int px = 0; px < pattern.Size.X; px++)
            {
                // Use the pattern's blocking logic
                if (!pattern.ShouldBlockAt(new Vector2I(px, py)))
                    continue;
                
                var markPos = position + new Vector2I(px, py);
                if (markPos.X < occupiedTiles.GetLength(1) && markPos.Y < occupiedTiles.GetLength(0))
                {
                    occupiedTiles[markPos.X, markPos.Y] = true;
                }
            }
        }
    }

    private Image? GetBaseTileImage()
    {
        if (SpriteRegion == null) return null;

        if (SpriteRegion.Layers == null || SpriteRegion.Layers.Length == 0) return null;

        // Use the same logic as SpriteRegion.CreateCompositeImage but for single tile
        var firstLayer = SpriteRegion.Layers[0];
        var textureData = GetTextureRegion(firstLayer);

        if (textureData?.Texture == null) return null;

        var sourceImage = textureData.Texture.GetImage();
        if (sourceImage == null) return null;

        var regionRect = new Rect2I(
            (int)textureData.Region.Position.X,
            (int)textureData.Region.Position.Y,
            (int)textureData.Region.Size.X,
            (int)textureData.Region.Size.Y
        );

        return sourceImage.GetRegion(regionRect);
    }

    private void RenderSpawnPattern(Image targetImage, TilePattern pattern, Vector2I gridPos, int tileSize)
    {
        if (SpriteRegion == null || TileSet == null) return;

        for (int patternY = 0; patternY < pattern.Size.Y; patternY++)
        {
            for (int patternX = 0; patternX < pattern.Size.X; patternX++)
            {
                var tile = pattern.GetTileAt(new Vector2I(patternX, patternY));
                if (tile == null) continue;

                var patternTileImage = GetPatternTileImage(TileSet, tile);
                if (patternTileImage == null) continue;

                var targetGridX = gridPos.X + patternX;
                var targetGridY = gridPos.Y + patternY;

                // Skip if outside grid
                var gridSize = targetImage.GetSize().X / tileSize;
                if (targetGridX >= gridSize || targetGridY >= gridSize) continue;

                var pixelPos = new Vector2I(targetGridX * tileSize, targetGridY * tileSize);

                // Resize pattern tile to match grid
                patternTileImage.Resize(tileSize, tileSize, Image.Interpolation.Nearest);

                // Blend over base tile
                targetImage.BlendRect(patternTileImage,
                    new Rect2I(Vector2I.Zero, new Vector2I(tileSize, tileSize)),
                    pixelPos);
            }
        }
    }

    private Image? GetPatternTileImage(TileSet tileSet, TilePlacement tile)
    {
        var source = tileSet.GetSource(tile.SourceId);
        if (source is not TileSetAtlasSource atlasSource) return null;

        var texture = atlasSource.Texture;
        if (texture == null) return null;

        var sourceImage = texture.GetImage();
        if (sourceImage == null) return null;

        var region = atlasSource.GetTileTextureRegion(tile.AtlasCoords);
        var regionRect = new Rect2I(
            (int)region.Position.X,
            (int)region.Position.Y,
            (int)region.Size.X,
            (int)region.Size.Y
        );

        return sourceImage.GetRegion(regionRect);
    }

    private LayerData? GetTextureRegion(TileReference tileRef)
    {
        var source = TileSet?.GetSource(tileRef.SourceId);
        if (source is not TileSetAtlasSource atlasSource) return null;

        var texture = atlasSource.Texture;
        if (texture == null) return null;

        var region = atlasSource.GetTileTextureRegion(tileRef.AtlasCoords);
        return new LayerData { Texture = texture, Region = region };
    }

    // WFC compatibility methods
    public bool CanConnectTo(SemanticTile other, Direction direction)
    {
        if (other == null) return false;

        var ourSocket = Sockets[(int)direction];
        var oppositeDirection = GetOppositeDirection(direction);
        var theirSocket = other.Sockets[(int)oppositeDirection];

        return SocketsCompatible(ourSocket, theirSocket);
    }

    private static bool SocketsCompatible(SocketType socket1, SocketType socket2)
    {
        if (socket1 == socket2) return true;
        if (socket1 == SocketType.Any || socket2 == SocketType.Any) return true;
        return false;
    }

    private static Direction GetOppositeDirection(Direction dir)
    {
        return dir switch
        {
            Direction.North => Direction.South,
            Direction.East => Direction.West,
            Direction.South => Direction.North,
            Direction.West => Direction.East,
            _ => throw new ArgumentException("Invalid direction")
        };
    }
}