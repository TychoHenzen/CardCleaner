#if TOOLS
using System.Collections.Generic;
using System.Text.Json.Serialization;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Addons.TileEditor;

internal static class TmxAtlasCompilerModels
{
    internal sealed class TsxLoadResult
    {
        internal bool Success { get; init; }
        internal string Error { get; init; } = "";
        internal List<TsxTileData> Tiles { get; init; } = new();
        internal List<TsxWangSetData> WangSets { get; init; } = new();
    }

    internal sealed class TmxSourceCollection
    {
        internal List<TsxTileData> Tiles { get; } = new();
        internal List<TsxWangSetData> WangSets { get; } = new();
    }

internal sealed class TsxSourceInfo
{
    internal int TileWidth { get; init; }
    internal int TileHeight { get; init; }
    internal int Columns { get; init; }
    internal float SourceScale { get; init; }
    internal Image Image { get; init; } = null!;
}

    internal sealed class TsxSourceReadResult
    {
        internal TsxSourceInfo? Source { get; init; }
        internal string Error { get; init; } = "";
        internal bool Success => Source != null;
    }

internal sealed class TilePropertyIndex
{
    internal Dictionary<int, Dictionary<string, string>> Properties { get; } = new();
    internal Dictionary<int, string> Classes { get; } = new();
}

internal sealed class TsxTileData
{
    internal int TileId { get; set; }
    internal string TsxPath { get; set; } = "";
    internal int AtlasX { get; set; }
    internal int AtlasY { get; set; }
    internal int SourceTileWidth { get; set; }
    internal int SourceTileHeight { get; set; }
    internal float SourceScale { get; set; }
    internal Image SourceImage { get; set; } = null!;
    internal Dictionary<string, string> Properties { get; set; } = new();
    internal string Id { get; set; } = "";
    internal string Layer { get; set; } = "terrain";
    internal int Dominance { get; set; }
    internal bool HasExplicitId { get; set; }
    internal bool HasClassAttribute { get; set; }
}

internal sealed class TsxWangSetData
{
    internal string Name { get; set; } = "";
    internal string Type { get; set; } = "corner";
    internal string TsxPath { get; set; } = "";
    internal int Columns { get; set; }
    internal float SourceScale { get; set; }
    internal Dictionary<string, string> Properties { get; set; } = new();
    internal Dictionary<int, List<int>> WangTiles { get; set; } = new();
    internal HashSet<int> AllMemberTileIds { get; set; } = new();
    internal Image SourceImage { get; set; } = null!;
    internal int SourceTileWidth { get; set; }
    internal int SourceTileHeight { get; set; }
    internal bool IsTransparent { get; set; }
    internal string OuterTerrain { get; set; } = "";
    internal string InnerTerrain { get; set; } = "";
}

internal sealed class PackedTsxTile
{
    internal TsxTileData Source { get; set; } = null!;
    internal int AtlasX { get; set; }
    internal int AtlasY { get; set; }
}

internal sealed class TilePackResult
{
    internal bool Success { get; init; }
    internal string Message { get; init; } = "";
    internal List<PackedTsxTile> PackedTiles { get; init; } = new();
    internal Vector2I AtlasSize { get; init; }
    internal Dictionary<string, Dictionary<string, TileAtlasRect>> Mapping { get; init; } = new();
}

internal sealed class CompositePackResult
{
    internal Image Atlas { get; init; } = null!;
    internal int CurrentX { get; init; }
    internal int CurrentY { get; init; }
}

internal sealed class CompositionResult
{
    internal Image Atlas { get; init; } = null!;
    internal CompiledTransitionMap TransitionMap { get; init; } = new();
    internal int CompositeCount { get; init; }
    internal int CurrentX { get; init; }
    internal int CurrentY { get; init; }
    internal int RowHeight { get; init; }
}

internal sealed class CompositionInput
{
    internal List<TsxTileData> BaseTerrains { get; init; } = new();
    internal List<TsxWangSetData> CompositableWangSets { get; init; } = new();
    internal List<TsxWangSetData> FixedWangSets { get; init; } = new();
    internal TilePackResult PackResult { get; init; } = new();
    internal Image Atlas { get; init; } = null!;
    internal Vector2I PackedAtlasSize { get; init; }
    internal int TargetTileSize { get; init; }
    internal int AtlasWidth { get; init; }
}

internal sealed class TerrainSelection
{
    internal List<TsxTileData> SimpleBaseTerrains { get; init; } = new();
    internal List<TsxTileData> WangSetSolidFills { get; init; } = new();
    internal List<TsxTileData> BaseTerrains { get; init; } = new();
    internal List<TsxWangSetData> CompositableWangSets { get; init; } = new();
    internal List<TsxWangSetData> FixedWangSets { get; init; } = new();
    internal List<TsxTileData> TilesToPack { get; init; } = new();
    internal int EstimatedCompositeCount { get; init; }
}

internal sealed class AtlasSaveResult
{
    internal bool Success { get; init; }
    internal string Message { get; init; } = "";
}

internal sealed class AtlasMappingData
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";

    [JsonPropertyName("atlas")]
    public AtlasInfo Atlas { get; set; } = new();

    [JsonPropertyName("sources")]
    public Dictionary<string, Dictionary<string, TileAtlasRect>> Sources { get; set; } = new();
}

internal sealed class AtlasInfo
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

internal sealed class CompositeAtlasState
{
    internal Image Atlas { get; set; } = null!;
    internal int CurrentX { get; set; }
    internal int CurrentY { get; set; }
    internal int RowHeight { get; init; }
    internal int AtlasWidth { get; init; }
    internal int TargetTileSize { get; init; }
    internal int CompositeCount { get; set; }
}

}
#endif
