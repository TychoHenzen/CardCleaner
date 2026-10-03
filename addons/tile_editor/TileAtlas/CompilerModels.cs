#if TOOLS
using System.Collections.Generic;
using System.Text.Json.Serialization;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Addons.TileEditor;

internal static class TileAtlasCompilerModels
{
    internal static class TileAtlasCompilerConstants
    {
        internal const int MaxAtlasSize = 16384;
        internal const int TilePadding = 0;
        internal const string CompiledAtlasDirectory = "res://Data/CompiledAtlas";
        internal const string AtlasFileName = "terrain_atlas.png";
        internal const string MappingFileName = "atlas_mapping.json";

        internal static int NextPowerOf2(int value)
        {
            var power = 1;
            while (power < value)
                power *= 2;
            return System.Math.Min(power, MaxAtlasSize);
        }
    }

    internal sealed class TerrainSelection
    {
        internal List<EditableTile> SimpleBaseTerrains { get; init; } = new();
        internal List<EditableTile> AutoTileSolidFills { get; init; } = new();
        internal List<EditableTile> BaseTerrains { get; init; } = new();
        internal List<EditableTile> CompositableAutoTiles { get; init; } = new();
        internal List<EditableTile> FixedAutoTiles { get; init; } = new();
    }

    internal sealed class TileAtlasPackResult
    {
        internal bool Success { get; init; }
        internal string Message { get; init; } = "";
        internal List<PackedTile> PackedTiles { get; init; } = new();
        internal Vector2I AtlasSize { get; init; }
        internal Dictionary<string, AtlasTileMapping> Mapping { get; init; } = new();
    }

    internal sealed class TileAtlasCompositionInput
    {
        internal List<EditableTile> BaseTerrains { get; init; } = new();
        internal List<EditableTile> CompositableAutoTiles { get; init; } = new();
        internal List<EditableTile> FixedAutoTiles { get; init; } = new();
        internal TileAtlasPackResult PackResult { get; init; } = new();
        internal Image Atlas { get; init; } = null!;
        internal TileSet TileSet { get; init; } = null!;
        internal Vector2I PackedAtlasSize { get; init; }
        internal Vector2I TileSize { get; init; }
        internal int AtlasWidth { get; init; }
    }

    internal sealed class TileAtlasCompositionResult
    {
        internal Image Atlas { get; init; } = null!;
        internal CompiledTransitionMap TransitionMap { get; init; } = new();
        internal int CompositeCount { get; init; }
        internal int CurrentX { get; init; }
        internal int CurrentY { get; init; }
        internal int RowHeight { get; init; }
    }

    internal sealed class CompositeAtlasState
    {
        internal Image Atlas { get; set; } = null!;
        internal int CurrentX { get; set; }
        internal int CurrentY { get; set; }
        internal int RowHeight { get; init; }
        internal int AtlasWidth { get; init; }
        internal int TileSize { get; init; }
        internal int CompositeCount { get; set; }
    }

    internal sealed class TileAtlasSaveResult
    {
        internal bool Success { get; init; }
        internal string Message { get; init; } = "";
    }

    internal sealed record TileRegion(
        int SourceId,
        int AtlasX,
        int AtlasY,
        int Width,
        int Height,
        float SourceScale);

    internal sealed record PackedTile(TileRegion Region, int AtlasX, int AtlasY);

    internal sealed class AtlasTileMapping : Dictionary<string, TileAtlasRect>
    {
    }

    internal sealed class TileAtlasMappingData
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0";

        [JsonPropertyName("atlas")]
        public TileAtlasInfo Atlas { get; set; } = new();

        [JsonPropertyName("sources")]
        public Dictionary<string, AtlasTileMapping> Sources { get; set; } = new();
    }

    internal sealed class TileAtlasInfo
    {
        [JsonPropertyName("path")]
        public string Path { get; set; } = "";

        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }

        [JsonPropertyName("tileSize")]
        public int TileSize { get; set; }
    }

    internal sealed class TileAtlasRect
    {
        [JsonPropertyName("x")]
        public int X { get; set; }

        [JsonPropertyName("y")]
        public int Y { get; set; }

        [JsonPropertyName("w")]
        public int W { get; set; }

        [JsonPropertyName("h")]
        public int H { get; set; }
    }
}
#endif
