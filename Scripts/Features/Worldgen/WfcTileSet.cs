using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Enum;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen;

[Tool]
[GlobalClass]
public partial class WfcTileSet : Resource
{
    [Export] public Godot.Collections.Array<WfcTile> Tiles { get; set; } = new();
    [Export] public Vector2I TileSize { get; set; } = new(64, 64);
    [Export] public int AtlasSize { get; set; } = 1024; // Max atlas dimension

    // Runtime generated data
    public TileSet GeneratedTileSet { get; private set; }
    public Dictionary<string, WfcTile> TilesByName { get; private set; } = new();

    private ImageTexture _atlasTexture;
    private TileSetAtlasSource _atlasSource;

    public WfcTileSet()
    {
        // Default constructor
    }

    // Generate the runtime TileSet and atlas
    public TileSet BuildRuntimeTileSet()
    {
        if (Tiles.Count == 0)
        {
            GD.PrintErr("WfcTileSet: No tiles to build");
            return null;
        }

        // Create atlas texture
        var atlasImage = CreateAtlasImage();
        if (atlasImage == null)
        {
            GD.PrintErr("WfcTileSet: Failed to create atlas");
            return null;
        }

        // Create atlas texture
        _atlasTexture = ImageTexture.CreateFromImage(atlasImage);

        // Create TileSet
        GeneratedTileSet = new TileSet();
        _atlasSource = new TileSetAtlasSource();
        _atlasSource.Texture = _atlasTexture;
        _atlasSource.TextureRegionSize = TileSize;
        int atlasIndex = 0;
        for (int i = 0; i < Tiles.Count; i++)
        {
            var tile = Tiles[i];

            if (tile.IsAnimated)
            {
                // Create multiple atlas entries for animated tile
                var frameCount = tile.SpriteRegion.Length;
                var animationFrames = new Vector2I[frameCount];

                for (int frame = 0; frame < frameCount; frame++)
                {
                    var atlasCoords = new Vector2I(atlasIndex % GetTilesPerRow(), atlasIndex / GetTilesPerRow());
                    _atlasSource.CreateTile(atlasCoords);
                    animationFrames[frame] = atlasCoords;
                    atlasIndex++;
                }

                tile.AtlasId = i;
                tile.AtlasCoords = animationFrames[0]; // Default to first frame
                tile.AnimationFrames = animationFrames;
            }
            else
            {
                // Single atlas entry for static tile
                var atlasCoords = new Vector2I(atlasIndex % GetTilesPerRow(), atlasIndex / GetTilesPerRow());
                _atlasSource.CreateTile(atlasCoords);
                tile.AtlasId = i;
                tile.AtlasCoords = atlasCoords;
                tile.AnimationFrames = new[] { atlasCoords };
                atlasIndex++;
            }

            // Index by name for easy lookup
            if (!string.IsNullOrEmpty(tile.TileName))
            {
                TilesByName[tile.TileName] = tile;
            }
        }

        GeneratedTileSet.AddSource(_atlasSource, 0);

        GD.Print($"WfcTileSet: Generated TileSet with {Tiles.Count} tiles");
        return GeneratedTileSet;
    }

    private Image CreateAtlasImage()
    {
        // Calculate total atlas entries needed
        int totalAtlasEntries = 0;
        foreach (var tile in Tiles)
        {
            totalAtlasEntries += tile.IsAnimated ? tile.SpriteRegion.Length : 1;
        }

        var tilesPerRow = GetTilesPerRow();
        var rows = Mathf.CeilToInt((float)totalAtlasEntries / tilesPerRow);
        var atlasWidth = tilesPerRow * TileSize.X;
        var atlasHeight = rows * TileSize.Y;

        if (atlasWidth > AtlasSize || atlasHeight > AtlasSize)
        {
            GD.PrintErr($"WfcTileSet: Atlas size {atlasWidth}x{atlasHeight} exceeds maximum {AtlasSize}");
            return null;
        }

        var atlasImage = Image.CreateEmpty(atlasWidth, atlasHeight, false, Image.Format.Rgba8);
        atlasImage.Fill(Colors.Transparent);

        int atlasIndex = 0;
        for (int i = 0; i < Tiles.Count; i++)
        {
            var tile = Tiles[i];
            if (tile.SpriteRegion == null || tile.SpriteRegion.Length == 0)
            {
                GD.PrintErr($"WfcTileSet: Tile {i} ({tile.TileName}) has no sprite regions");
                continue;
            }

            if (tile.IsAnimated)
            {
                // Process each animation frame (SpriteRegion)
                for (int frame = 0; frame < tile.SpriteRegion.Length; frame++)
                {
                    var spriteRegion = tile.SpriteRegion[frame];
                    if (spriteRegion?.PreviewTexture == null)
                    {
                        GD.PrintErr($"WfcTileSet: Tile {i} frame {frame} has no preview texture");
                        continue;
                    }

                    var frameImage = spriteRegion.PreviewTexture.GetImage();

                    // Resize to tile size if necessary
                    if (frameImage.GetSize() != TileSize)
                    {
                        frameImage.Resize(TileSize.X, TileSize.Y, Image.Interpolation.Nearest);
                    }

                    // Calculate position in atlas
                    var atlasX = (atlasIndex % tilesPerRow) * TileSize.X;
                    var atlasY = (atlasIndex / tilesPerRow) * TileSize.Y;

                    // Blit to atlas
                    atlasImage.BlitRect(frameImage, new Rect2I(Vector2I.Zero, TileSize),
                        new Vector2I(atlasX, atlasY));
                    atlasIndex++;
                }
            }
            else
            {
                // Single frame for static tile (use first SpriteRegion)
                var spriteRegion = tile.SpriteRegion[0];
                if (spriteRegion?.PreviewTexture == null)
                {
                    GD.PrintErr($"WfcTileSet: Tile {i} has no preview texture");
                    continue;
                }

                var spriteImage = spriteRegion.PreviewTexture.GetImage();

                // Resize to tile size if necessary
                if (spriteImage.GetSize() != TileSize)
                {
                    spriteImage.Resize(TileSize.X, TileSize.Y, Image.Interpolation.Nearest);
                }

                // Calculate position in atlas
                var atlasX = (atlasIndex % tilesPerRow) * TileSize.X;
                var atlasY = (atlasIndex / tilesPerRow) * TileSize.Y;

                // Blit to atlas
                atlasImage.BlitRect(spriteImage, new Rect2I(Vector2I.Zero, TileSize),
                    new Vector2I(atlasX, atlasY));
                atlasIndex++;
            }
        }

        return atlasImage;
    }

    private int GetTilesPerRow()
    {
        return AtlasSize / TileSize.X;
    }

    // Find tile by name
    public WfcTile GetTileByName(string name)
    {
        return TilesByName.GetValueOrDefault(name);
    }

    // Get all tiles with a specific socket type
    public List<WfcTile> GetTilesWithSocket(SocketType socketType, Direction direction)
    {
        return Tiles.Where(tile => tile.Sockets[(int)direction] == socketType).ToList();
    }

    // Validate tile set for common issues
    public List<string> ValidateTileSet()
    {
        var issues = new List<string>();

        // Check for tiles without sprites
        var tilesWithoutSprites = Tiles.Where(t => t.SpriteRegion.Length == 0 ||
                                                   t.SpriteRegion.Any(reg =>
                                                       reg.Layers.Any(data => data.Texture == null))).ToList();
        if (tilesWithoutSprites.Count != 0)
        {
            issues.Add($"{tilesWithoutSprites.Count} tiles missing sprite regions");
        }

        // Check for duplicate names
        var nameGroups = Tiles.Where(t => !string.IsNullOrEmpty(t.TileName))
            .GroupBy(t => t.TileName)
            .Where(g => g.Count() > 1);

        issues.AddRange(
            nameGroups
                .Select(group => $"Duplicate tile name: {group.Key}"));

        // Check socket connectivity
        var socketTypes = Enum.GetValues<SocketType>();
        issues.AddRange(socketTypes
            .Select(socketType => new { socketType, hasOutput = Tiles.Any(t => t.Sockets.Contains(socketType)) })
            .Where(@t1 => !@t1.hasOutput && @t1.socketType != SocketType.Mixed)
            .Select(@t1 => $"No tiles have socket type: {@t1.socketType}"));

        return issues;
    }
}