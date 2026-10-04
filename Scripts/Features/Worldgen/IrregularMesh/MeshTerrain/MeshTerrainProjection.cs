using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.MeshTerrain;

internal sealed class MeshTerrainProjection
{
    private readonly ITileRegistry? _tileRegistry;
    private readonly Dictionary<string, int>? _tileToTerrainType;

    public MeshTerrainProjection(
        ITileRegistry? tileRegistry,
        Dictionary<string, int>? tileToTerrainType)
    {
        _tileRegistry = tileRegistry;
        _tileToTerrainType = tileToTerrainType;
    }

    public static void MapBackgroundToQuads(
        IrregularMesh mesh,
        SimpleMapData backgroundData,
        (Vector2 Min, Vector2 Max) bounds)
    {
        var wfcSize = backgroundData.Size;
        var meshSize = bounds.Max - bounds.Min;

        foreach (var quad in mesh.Quads)
        {
            var gridPosition = ToGridPosition(quad.Centroid, bounds.Min, meshSize, wfcSize);
            quad.BackgroundTileId = backgroundData.TileIds[gridPosition.Y, gridPosition.X];
        }

        GD.Print($"[MeshTerrainGen] Mapped {mesh.Quads.Count} quads with backgrounds");
    }

    public void MapForegroundToVertices(
        IrregularMesh mesh,
        SimpleMapData foregroundData,
        (Vector2 Min, Vector2 Max) bounds)
    {
        var wfcSize = foregroundData.Size;
        var meshSize = bounds.Max - bounds.Min;
        var autoTileCount = 0;

        foreach (var vertex in mesh.Vertices)
        {
            var gridPosition = ToGridPosition(vertex.Position, bounds.Min, meshSize, wfcSize);
            var tileId = foregroundData.TileIds[gridPosition.Y, gridPosition.X];
            var tile = _tileRegistry?.GetTile(tileId);

            if (tile?.HasAutoTileVariants == true)
            {
                vertex.ForegroundTileId = tileId;
                vertex.TileId = tileId;
                autoTileCount++;
            }
            else
            {
                vertex.ForegroundTileId = null;
                vertex.TileId = null;
            }

            vertex.TerrainType = tile?.IsPassable == true ? 1 : 0;
        }

        GD.Print(
            $"[MeshTerrainGen] Mapped {autoTileCount}/{mesh.Vertices.Count} vertices " +
            "with auto-tiles (legacy projection)");
    }

    public static void ClearForeground(IrregularMesh mesh)
    {
        foreach (var vertex in mesh.Vertices)
        {
            vertex.ForegroundTileId = null;
            vertex.TileId = null;
            vertex.TerrainType = 1;
        }
    }

    public void ApplyFallbackTerrain(IrregularMesh mesh)
    {
        var fallbackTile = _tileRegistry?.GetAllTiles()
            .FirstOrDefault(tile => tile.IsPassable && !tile.HasAutoTileVariants);
        var fallbackId = fallbackTile?.Id ?? "floor";
        var fallbackTerrainType = GetFallbackTerrainType(fallbackId);

        foreach (var quad in mesh.Quads)
            quad.BackgroundTileId = fallbackId;

        foreach (var vertex in mesh.Vertices)
        {
            vertex.TileId = null;
            vertex.ForegroundTileId = null;
            vertex.TerrainType = fallbackTerrainType;
        }

        mesh.UpdateAllCachedProperties();
        GD.PrintErr("[MeshTerrainGen] Applied fallback terrain");
    }

    private int GetFallbackTerrainType(string fallbackId)
    {
        return _tileToTerrainType != null &&
               _tileToTerrainType.TryGetValue(fallbackId, out var mappedType)
            ? mappedType
            : 1;
    }

    private static Vector2I ToGridPosition(
        Vector2 position,
        Vector2 minPosition,
        Vector2 meshSize,
        Vector2I wfcSize)
    {
        var normalizedX = (position.X - minPosition.X) / meshSize.X;
        var normalizedY = (position.Y - minPosition.Y) / meshSize.Y;
        var gridX = Mathf.Clamp((int)(normalizedX * wfcSize.X), 0, wfcSize.X - 1);
        var gridY = Mathf.Clamp((int)(normalizedY * wfcSize.Y), 0, wfcSize.Y - 1);
        return new Vector2I(gridX, gridY);
    }
}
