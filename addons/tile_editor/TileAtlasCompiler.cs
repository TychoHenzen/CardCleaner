#if TOOLS
using System.IO;
using Godot;
using static CardCleaner.Addons.TileEditor.TileAtlasCompilerModels;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// Compiles tiles from a TileSet into a single atlas texture with coordinate mapping.
/// The compiled atlas and mapping file are used at runtime instead of loading
/// hundreds of individual source textures.
/// </summary>
public class TileAtlasCompiler
{
    private readonly TileAtlasTerrainSelector _terrainSelector = new();
    private readonly TileAtlasTransitionValidator _transitionValidator = new();
    private readonly TileAtlasPacker _atlasPacker = new();
    private readonly TileAtlasImageOperations _imageOperations = new();
    private readonly TileAtlasTransitionComposer _transitionComposer = new();
    private readonly TileAtlasOutputWriter _outputWriter = new();

    /// <summary>
    /// Compiles all tiles from the TileEditorService into a single atlas.
    /// For compositable auto-tiles (OuterTerrainId="*"), generates N×M composite variants
    /// by compositing each border onto each base terrain.
    /// Returns the compilation status and message.
    /// </summary>
    public TileAtlasCompilationResult CompileAtlas(TileEditorService service)
    {
        if (service.TileSet == null)
            return new TileAtlasCompilationResult(false, "No TileSet loaded");

        foreach (var warning in _transitionValidator.Validate(service.AllTiles))
            GD.PrintErr($"[TileAtlasCompiler] {warning}");

        var tileSet = service.TileSet;
        var tileSize = tileSet.TileSize;
        EnsureOutputDirectory();
        var selection = _terrainSelector.Select(service.AllTiles);
        LogTerrainSelection(selection);

        var tilesToPack = _terrainSelector.CollectTilesToPack(service.AllTiles);
        if (tilesToPack.Count == 0)
            return new TileAtlasCompilationResult(false, "No tiles to compile");

        GD.Print($"[TileAtlasCompiler] Packing {tilesToPack.Count} base tile regions");
        var packResult = _atlasPacker.PackTiles(tilesToPack, tileSize);
        if (!packResult.Success)
            return new TileAtlasCompilationResult(false, packResult.Message);

        var atlasPath = $"{TileAtlasCompilerConstants.CompiledAtlasDirectory}/"
            + TileAtlasCompilerConstants.AtlasFileName;
        var atlasImage = _imageOperations.CreateAtlasImage(
            packResult.PackedTiles,
            packResult.AtlasSize,
            tileSet,
            tileSize);
        if (atlasImage == null)
            return new TileAtlasCompilationResult(
                false,
                "Failed to create atlas image");

        var composition = _transitionComposer.Generate(new TileAtlasCompositionInput
        {
            BaseTerrains = selection.BaseTerrains,
            CompositableAutoTiles = selection.CompositableAutoTiles,
            FixedAutoTiles = selection.FixedAutoTiles,
            PackResult = packResult,
            Atlas = atlasImage,
            TileSet = tileSet,
            PackedAtlasSize = packResult.AtlasSize,
            TileSize = tileSize,
            AtlasWidth = packResult.AtlasSize.X
        });
        GD.Print(
            $"[TileAtlasCompiler] Generated {composition.CompositeCount} "
            + "composite tiles");

        var result = SaveOutputs(composition, packResult, tileSize, tilesToPack.Count, atlasPath);
        return new TileAtlasCompilationResult(result.Success, result.Message);
    }

    private TileAtlasSaveResult SaveOutputs(
        TileAtlasCompositionResult composition,
        TileAtlasPackResult packResult,
        Vector2I tileSize,
        int packedTileCount,
        string atlasPath)
    {
        var atlasImage = _imageOperations.TrimAtlas(composition, packResult.AtlasSize.Y);
        var atlasResult = _outputWriter.SaveAtlasImage(atlasImage, atlasPath);
        if (!atlasResult.Success)
            return atlasResult;

        var finalAtlasSize = new Vector2I(atlasImage.GetWidth(), atlasImage.GetHeight());
        var mappingPath = $"{TileAtlasCompilerConstants.CompiledAtlasDirectory}/"
            + TileAtlasCompilerConstants.MappingFileName;
        var mappingResult = _outputWriter.SaveAtlasMapping(
            packResult.Mapping,
            finalAtlasSize,
            tileSize,
            atlasPath,
            mappingPath);
        if (!mappingResult.Success)
            return mappingResult;

        var transitionMapPath =
            $"{TileAtlasCompilerConstants.CompiledAtlasDirectory}/transition_map.json";
        var transitionResult = _outputWriter.SaveTransitionMap(
            composition.TransitionMap,
            transitionMapPath);
        if (!transitionResult.Success)
            return transitionResult;

        var totalTiles = packedTileCount + composition.CompositeCount;
        return new TileAtlasSaveResult
        {
            Success = true,
            Message = $"Compiled {totalTiles} tiles ({composition.CompositeCount} composites) "
                + $"to {atlasPath}"
        };
    }

    private static void LogTerrainSelection(TerrainSelection selection)
    {
        GD.Print(
            $"[TileAtlasCompiler] Found {selection.SimpleBaseTerrains.Count} "
            + $"simple base terrains, {selection.AutoTileSolidFills.Count} auto-tile solid fills, "
            + $"{selection.CompositableAutoTiles.Count} compositable auto-tiles, "
            + $"{selection.FixedAutoTiles.Count} fixed auto-tiles");
    }

    private static void EnsureOutputDirectory()
    {
        var outputDirectory = ProjectSettings.GlobalizePath(
            TileAtlasCompilerConstants.CompiledAtlasDirectory);
        if (!Directory.Exists(outputDirectory))
            Directory.CreateDirectory(outputDirectory);
    }
}
#endif
