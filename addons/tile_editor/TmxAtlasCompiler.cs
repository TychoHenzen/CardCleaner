#if TOOLS
using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using static CardCleaner.Addons.TileEditor.TmxAtlasCompilerModels;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// Compiles tiles from TMX/TSX source files into a single atlas texture with coordinate mapping.
/// This is an alternative to TileAtlasCompiler that reads directly from Tiled files
/// instead of the JSON-based tiles.json.
/// </summary>
public class TmxAtlasCompiler
{
    private const string CompiledAtlasDir = "res://Data/CompiledAtlas";
    private const string AtlasFileName = "terrain_atlas.png";

    private readonly TmxTsxLoader _sourceLoader = new();
    private readonly TmxAtlasPacker _atlasPacker = new();
    private readonly TmxAtlasOutputWriter _outputWriter = new();

    /// <summary>
    /// Compiles all tiles from TSX files in the specified directory into a single atlas.
    /// Returns a result containing success and a message.
    /// </summary>
    /// <param name="tiledDirectory">Directory containing TSX files (e.g., "res://Data/Tiled")</param>
    /// <param name="targetTileSize">Target tile size in output atlas (default 16)</param>
    public TmxCompilationResult CompileFromTmx(string tiledDirectory, int targetTileSize = 16)
    {
        var absoluteDirectory = ProjectSettings.GlobalizePath(tiledDirectory);
        if (!Directory.Exists(absoluteDirectory))
            return Failure($"Directory not found: {absoluteDirectory}");

        var tsxFiles = Directory.GetFiles(absoluteDirectory, "*.tsx", SearchOption.AllDirectories);
        if (tsxFiles.Length == 0)
            return Failure("No TSX files found in directory");

        GD.Print($"[TmxAtlasCompiler] Found {tsxFiles.Length} TSX file(s)");
        EnsureOutputDirectory();
        var sources = LoadSources(tsxFiles, targetTileSize);
        if (sources.Tiles.Count == 0)
            return Failure("No tiles found in TSX files");

        var selection = TmxTerrainSelector.Create(sources.Tiles, sources.WangSets);
        LogTerrainSelection(selection);
        var totalEstimatedTiles = selection.TilesToPack.Count + selection.EstimatedCompositeCount;
        var atlasWidth = _atlasPacker.CalculateOptimalAtlasWidth(totalEstimatedTiles, targetTileSize);
        GD.Print(
            $"[TmxAtlasCompiler] Estimated {totalEstimatedTiles} total tiles, "
            + $"using atlas width {atlasWidth}px");

        var packResult = _atlasPacker.PackTiles(selection.TilesToPack, targetTileSize, atlasWidth);
        if (!packResult.Success)
            return Failure(packResult.Message);

        var atlasImage = _atlasPacker.CreateAtlasImage(
            packResult.PackedTiles,
            packResult.AtlasSize,
            targetTileSize);
        if (atlasImage == null)
            return Failure("Failed to create atlas image");

        var composer = new TmxTransitionComposer(_atlasPacker);
        var composition = composer.Generate(new CompositionInput
        {
            BaseTerrains = selection.BaseTerrains,
            CompositableWangSets = selection.CompositableWangSets,
            FixedWangSets = selection.FixedWangSets,
            PackResult = packResult,
            Atlas = atlasImage,
            PackedAtlasSize = packResult.AtlasSize,
            TargetTileSize = targetTileSize,
            AtlasWidth = atlasWidth
        });
        GD.Print($"[TmxAtlasCompiler] Generated {composition.CompositeCount} composite tiles");

        return SaveOutputs(
            composition,
            packResult,
            targetTileSize,
            selection.TilesToPack.Count,
            tsxFiles.Length);
    }

    private TmxSourceCollection LoadSources(string[] tsxFiles, int targetTileSize)
    {
        var sources = new TmxSourceCollection();
        foreach (var tsxPath in tsxFiles)
        {
            var result = _sourceLoader.Load(tsxPath, targetTileSize);
            if (!result.Success)
            {
                GD.PrintErr($"[TmxAtlasCompiler] Failed to load {tsxPath}: {result.Error}");
                continue;
            }

            sources.Tiles.AddRange(result.Tiles);
            sources.WangSets.AddRange(result.WangSets);
            GD.Print(
                $"[TmxAtlasCompiler] Loaded {result.Tiles.Count} tiles, "
                + $"{result.WangSets.Count} wang sets from {Path.GetFileName(tsxPath)}");
        }

        return sources;
    }

    private static void LogTerrainSelection(TerrainSelection selection)
    {
        GD.Print(
            $"[TmxAtlasCompiler] Found {selection.SimpleBaseTerrains.Count} simple base terrains, "
            + $"{selection.WangSetSolidFills.Count} wang set solid fills, "
            + $"{selection.CompositableWangSets.Count} compositable wang sets, "
            + $"{selection.FixedWangSets.Count} fixed wang sets");
    }

    private TmxCompilationResult SaveOutputs(
        CompositionResult composition,
        TilePackResult packResult,
        int targetTileSize,
        int packedTileCount,
        int sourceFileCount)
    {
        var atlasImage = TrimAtlas(composition, packResult.AtlasSize.Y);
        var atlasPath = $"{CompiledAtlasDir}/{AtlasFileName}";
        var atlasResult = _outputWriter.SaveAtlasImage(atlasImage, atlasPath);
        if (!atlasResult.Success)
            return Failure(atlasResult.Message);

        var finalAtlasSize = new Vector2I(atlasImage.GetWidth(), atlasImage.GetHeight());
        GD.Print($"[TmxAtlasCompiler] Saved atlas {finalAtlasSize.X}x{finalAtlasSize.Y} to {atlasPath}");
        var mappingResult = _outputWriter.SaveAtlasMapping(
            packResult.Mapping,
            finalAtlasSize,
            targetTileSize,
            atlasPath);
        if (!mappingResult.Success)
            return Failure(mappingResult.Message);

        var transitionResult = _outputWriter.SaveTransitionMap(composition.TransitionMap);
        if (!transitionResult.Success)
            return Failure(transitionResult.Message);

        var totalTiles = packedTileCount + composition.CompositeCount;
        return new TmxCompilationResult(
            true,
            $"Compiled {totalTiles} tiles ({composition.CompositeCount} composites) "
            + $"from {sourceFileCount} TSX file(s) to {atlasPath}");
    }

    private static Image TrimAtlas(CompositionResult composition, int packedAtlasHeight)
    {
        var finalHeight = composition.CurrentY
            + (composition.CurrentX > 0 ? composition.RowHeight : 0);
        finalHeight = NextPowerOf2(Math.Max(finalHeight, packedAtlasHeight));
        if (finalHeight >= composition.Atlas.GetHeight())
            return composition.Atlas;

        var trimmedAtlas = Image.CreateEmpty(
            composition.Atlas.GetWidth(),
            finalHeight,
            false,
            Image.Format.Rgba8);
        trimmedAtlas.BlitRect(
            composition.Atlas,
            new Rect2I(0, 0, composition.Atlas.GetWidth(), finalHeight),
            Vector2I.Zero);
        return trimmedAtlas;
    }

    private static int NextPowerOf2(int value)
    {
        var power = 1;
        while (power < value)
            power *= 2;
        return Math.Min(power, 16384);
    }

    private static void EnsureOutputDirectory()
    {
        var outputDirectory = ProjectSettings.GlobalizePath(CompiledAtlasDir);
        if (!Directory.Exists(outputDirectory))
            Directory.CreateDirectory(outputDirectory);
    }

    private static TmxCompilationResult Failure(string message)
        => new(false, message);
}
#endif
