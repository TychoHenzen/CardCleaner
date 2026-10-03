#if TOOLS
using System.Collections.Generic;
using System.Linq;
using Godot;
using static CardCleaner.Addons.TileEditor.TileAtlasCompilerModels;

namespace CardCleaner.Addons.TileEditor;

internal sealed class TileAtlasPacker
{
    internal TileAtlasPackResult PackTiles(List<TileRegion> tiles, Vector2I tileSize)
    {
        var state = new PackingState(tileSize);
        var sortedTiles = tiles
            .OrderByDescending(tile => tile.Height)
            .ThenByDescending(tile => tile.Width)
            .ToList();

        foreach (var tile in sortedTiles)
        {
            var tilePixelWidth = tile.Width * state.PaddedTileSize;
            var tilePixelHeight = tile.Height * state.PaddedTileSize;
            if (state.CurrentX + tilePixelWidth > TileAtlasCompilerConstants.MaxAtlasSize)
                state.StartRow();

            if (state.CurrentY + tilePixelHeight > TileAtlasCompilerConstants.MaxAtlasSize)
            {
                return new TileAtlasPackResult
                {
                    Message = $"Atlas exceeds {TileAtlasCompilerConstants.MaxAtlasSize}x"
                        + $"{TileAtlasCompilerConstants.MaxAtlasSize} - need multi-atlas support",
                    PackedTiles = state.PackedTiles,
                    Mapping = state.Mapping
                };
            }

            state.Add(tile, tilePixelWidth, tilePixelHeight);
        }

        return new TileAtlasPackResult
        {
            Success = true,
            PackedTiles = state.PackedTiles,
            AtlasSize = new Vector2I(
                TileAtlasCompilerConstants.NextPowerOf2(state.MaxRowWidth),
                TileAtlasCompilerConstants.NextPowerOf2(state.CurrentY + state.RowHeight)),
            Mapping = state.Mapping
        };
    }

    private sealed class PackingState
    {
        internal PackingState(Vector2I tileSize)
        {
            TileSize = tileSize;
            PaddedTileSize = tileSize.X + TileAtlasCompilerConstants.TilePadding * 2;
        }

        internal Vector2I TileSize { get; }
        internal int PaddedTileSize { get; }
        internal int CurrentX { get; private set; }
        internal int CurrentY { get; private set; }
        internal int RowHeight { get; private set; }
        internal int MaxRowWidth { get; private set; }
        internal List<PackedTile> PackedTiles { get; } = new();
        internal Dictionary<string, AtlasTileMapping> Mapping { get; } = new();

        internal void StartRow()
        {
            CurrentX = 0;
            CurrentY += RowHeight;
            RowHeight = 0;
        }

        internal void Add(TileRegion tile, int tilePixelWidth, int tilePixelHeight)
        {
            PackedTiles.Add(new PackedTile(tile, CurrentX, CurrentY));
            AddMapping(tile);
            CurrentX += tilePixelWidth;
            RowHeight = System.Math.Max(RowHeight, tilePixelHeight);
            MaxRowWidth = System.Math.Max(MaxRowWidth, CurrentX);
        }

        private void AddMapping(TileRegion tile)
        {
            var sourceKey = tile.SourceId.ToString();
            if (!Mapping.TryGetValue(sourceKey, out var sourceMapping))
            {
                sourceMapping = new AtlasTileMapping();
                Mapping[sourceKey] = sourceMapping;
            }

            var coordKey = $"{tile.AtlasX},{tile.AtlasY}";
            sourceMapping[coordKey] = new TileAtlasRect
            {
                X = (CurrentX + TileAtlasCompilerConstants.TilePadding) / TileSize.X,
                Y = (CurrentY + TileAtlasCompilerConstants.TilePadding) / TileSize.Y,
                W = tile.Width,
                H = tile.Height
            };
        }
    }
}
#endif
