using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services.SimpleMapGeneratorSupport;

internal sealed class SimpleMapWfcGenerator
{
    private readonly SimpleMapGenerationContext _context;

    public SimpleMapWfcGenerator(SimpleMapGenerationContext context)
    {
        _context = context;
    }

    public TwoPhaseWfcResult Generate(Vector2I size)
    {
        var layers = new WfcLayerBuffers(size);
        if (_context.WfcGenerator == null || _context.BiomeRegistry == null)
        {
            GenerateFallback(size, layers);
            return ToResult(layers);
        }

        _context.WfcGenerator.MaxRetries = _context.MaxWfcRetries;
        GenerateBackground(size, layers);
        GenerateForeground(size, layers);
        MergeLayers(size, layers);
        return ToResult(layers);
    }

    private void GenerateFallback(Vector2I size, WfcLayerBuffers layers)
    {
        ILog.Print("[WFC] No WFC generator, using random fallback");
        for (var y = 0; y < size.Y; y++)
        for (var x = 0; x < size.X; x++)
        {
            var biome = _context.BiomeProvider.GetBiomeAt(new Vector2I(x, y));
            var tile = biome.SelectPassableTile(_context.Rng) ?? _context.DefaultPassableTileId;
            layers.BackgroundLayer[y, x] = tile;
            layers.ForegroundLayer[y, x] = "";
            layers.MergedGrid[y, x] = tile;
        }
    }

    private void GenerateBackground(Vector2I size, WfcLayerBuffers layers)
    {
        var result = _context.WfcGenerator!.GenerateMultiBiome(
            _context.BiomeRegistry!,
            position => _context.BiomeProvider.GetBiomeAt(position),
            size,
            _context.Rng.Randi(),
            _context.Gradient,
            tile => !tile.HasAutoTileVariants);

        if (result.Success && result.TileIds != null)
        {
            ILog.Print($"[WFC] Background phase succeeded in {result.Iterations} iterations");
            CopyLayer(result.TileIds, layers.BackgroundLayer, size);
            return;
        }

        ILog.Print(
            $"[WFC] Background phase failed: {result.ErrorMessage}, " +
            "using default passable fallback");
        FillLayer(layers.BackgroundLayer, size, _context.DefaultPassableTileId);
    }

    private void GenerateForeground(Vector2I size, WfcLayerBuffers layers)
    {
        var result = _context.WfcGenerator!.GenerateMultiBiome(
            _context.BiomeRegistry!,
            position => _context.BiomeProvider.GetBiomeAt(position),
            size,
            _context.Rng.Randi(),
            _context.Gradient);

        if (result.Success && result.TileIds != null)
        {
            ILog.Print($"[WFC] Foreground phase succeeded in {result.Iterations} iterations");
            CopyAutoTileLayer(result.TileIds, layers.ForegroundLayer, size);
            return;
        }

        ILog.Print(
            $"[WFC] Foreground phase failed: {result.ErrorMessage}, " +
            "using empty foreground");
        FillLayer(layers.ForegroundLayer, size, "");
    }

    private void CopyAutoTileLayer(string[,] source, string[,] destination, Vector2I size)
    {
        var autoTileCount = 0;
        for (var y = 0; y < size.Y; y++)
        for (var x = 0; x < size.X; x++)
        {
            var tileId = source[y, x];
            var tile = _context.TileRegistry.GetTile(tileId);
            if (tile?.HasAutoTileVariants == true)
            {
                destination[y, x] = tileId;
                autoTileCount++;
            }
            else
            {
                destination[y, x] = "";
            }
        }

        ILog.Print($"[WFC] Foreground contains {autoTileCount} auto-tile cells");
    }

    private static void CopyLayer(string[,] source, string[,] destination, Vector2I size)
    {
        for (var y = 0; y < size.Y; y++)
        for (var x = 0; x < size.X; x++)
            destination[y, x] = source[y, x];
    }

    private static void FillLayer(string[,] layer, Vector2I size, string tileId)
    {
        for (var y = 0; y < size.Y; y++)
        for (var x = 0; x < size.X; x++)
            layer[y, x] = tileId;
    }

    private static void MergeLayers(Vector2I size, WfcLayerBuffers layers)
    {
        for (var y = 0; y < size.Y; y++)
        for (var x = 0; x < size.X; x++)
        {
            layers.MergedGrid[y, x] = !string.IsNullOrEmpty(layers.ForegroundLayer[y, x])
                ? layers.ForegroundLayer[y, x]
                : layers.BackgroundLayer[y, x];
        }
    }

    private static TwoPhaseWfcResult ToResult(WfcLayerBuffers layers)
    {
        return new TwoPhaseWfcResult(
            layers.BackgroundLayer,
            layers.ForegroundLayer,
            layers.MergedGrid);
    }
}
