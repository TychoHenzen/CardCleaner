#if TOOLS
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CardCleaner.Addons.TileEditor;

public partial class TileEditorService
{
    /// <summary>
    /// Get the texture for a tile from the TileSet
    /// </summary>
    public Texture2D? GetTileTexture(EditableTile tile)
    {
        if (!IsTileSetValid())
            return null;

        try
        {
            var source = _tileSet!.GetSource(tile.SourceId) as TileSetAtlasSource;
            return source?.Texture;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[TileEditorService] Failed to get tile texture: {ex.Message}");
            return null;
        }
    }

    private bool IsTileSetValid()
    {
        if (_tileSet == null)
            return TryReloadTileSet();

        try
        {
            if (GodotObject.IsInstanceValid(_tileSet))
                return true;
        }
        catch
        {
        }

        _tileSet = null;
        return TryReloadTileSet();
    }

    private bool TryReloadTileSet()
    {
        if (string.IsNullOrEmpty(TilesetPath))
            return false;

        try
        {
            if (!ResourceLoader.Exists(TilesetPath))
                return false;

            _tileSet = ResourceLoader.Load<TileSet>(TilesetPath);
            if (_tileSet == null)
                return false;

            GD.Print(
                $"[TileEditorService] Reloaded TileSet after assembly reload from {TilesetPath}");
            return true;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[TileEditorService] Failed to reload TileSet: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Get the texture region for a tile (supports multi-tile sizes and source scaling)
    /// </summary>
    public Rect2I GetTileTextureRegion(EditableTile tile)
    {
        if (!IsTileSetValid())
            return DefaultTileRegion();

        try
        {
            var source = _tileSet!.GetSource(tile.SourceId) as TileSetAtlasSource;
            if (source == null)
                return DefaultTileRegion();

            var actualTileSize = GetSourceTileSize(tile);
            var atlasCoords = new Vector2I(tile.AtlasX, tile.AtlasY);
            var regionSize = new Vector2I(
                actualTileSize.X * tile.SizeX,
                actualTileSize.Y * tile.SizeY);
            return new Rect2I(atlasCoords * actualTileSize, regionSize);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[TileEditorService] Failed to get texture region: {ex.Message}");
            return DefaultTileRegion();
        }
    }

    private Vector2I GetSourceTileSize(EditableTile tile)
    {
        var baseTileSize = _tileSet!.TileSize;
        return new Vector2I(
            (int)(baseTileSize.X / tile.SourceScale),
            (int)(baseTileSize.Y / tile.SourceScale));
    }

    private static Rect2I DefaultTileRegion() => new(0, 0, 16, 16);

    /// <summary>
    /// Get all available TileSetAtlasSource entries with descriptive info
    /// </summary>
    public List<AtlasSourceInfo> GetAvailableAtlasSources()
    {
        var sources = new List<AtlasSourceInfo>();
        if (!IsTileSetValid())
            return sources;

        try
        {
            for (var index = 0; index < _tileSet!.GetSourceCount(); index++)
            {
                var sourceId = _tileSet.GetSourceId(index);
                var source = _tileSet.GetSource(sourceId) as TileSetAtlasSource;
                if (source == null)
                    continue;

                sources.Add(new AtlasSourceInfo
                {
                    SourceId = sourceId,
                    DisplayName = BuildSourceDisplayName(sourceId, source),
                    Source = source
                });
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[TileEditorService] Failed to get atlas sources: {ex.Message}");
        }

        return sources;
    }

    private static string BuildSourceDisplayName(int sourceId, TileSetAtlasSource source)
    {
        var textureName = source.Texture?.ResourcePath ?? "Unknown";
        if (textureName.Contains('/'))
            textureName = textureName.GetFile();
        return $"Source {sourceId}: {textureName}";
    }

    /// <summary>
    /// Get a specific TileSetAtlasSource by ID
    /// </summary>
    public TileSetAtlasSource? GetAtlasSource(int sourceId)
    {
        if (!IsTileSetValid())
            return null;

        try
        {
            return _tileSet!.GetSource(sourceId) as TileSetAtlasSource;
        }
        catch (Exception ex)
        {
            GD.PrintErr(
                $"[TileEditorService] Failed to get atlas source {sourceId}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Get the set of all SourceId values currently referenced by tiles
    /// </summary>
    public HashSet<int> GetUsedSourceIds()
    {
        return _tiles.Values.Select(tile => tile.SourceId).ToHashSet();
    }

    /// <summary>
    /// Remove a source from the TileSet and save the resource
    /// </summary>
    public bool RemoveSource(int sourceId)
    {
        if (!IsTileSetValid())
            return false;

        try
        {
            if (_tiles.Values.Any(tile => tile.SourceId == sourceId))
            {
                GD.PrintErr(
                    $"[TileEditorService] Cannot remove source {sourceId}: tiles are using it");
                return false;
            }

            _tileSet!.RemoveSource(sourceId);
            var saveResult = ResourceSaver.Save(_tileSet, TilesetPath);
            if (saveResult == Error.Ok)
            {
                GD.Print($"[TileEditorService] Removed source {sourceId} and saved TileSet");
                return true;
            }

            GD.PrintErr(
                $"[TileEditorService] Failed to save TileSet after removing source: {saveResult}");
            return false;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[TileEditorService] Failed to remove source: {ex.Message}");
            return false;
        }
    }
}
#endif
