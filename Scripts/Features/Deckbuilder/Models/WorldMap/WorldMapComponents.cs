using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Deckbuilder.Components;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Models;

/// <summary>
/// The tile-registry dependent rendering components of the world map (terrain, fog, biome overlay) and the
/// tile size they share. Components stay null until a tile registry and a viewport are available.
/// </summary>
internal sealed class WorldMapComponents
{
    // Visual constants - default, overridden by TilesetConfig when available
    private const int DefaultTileSize = 16;

    private readonly SimpleWorldMapScreen _screen;

    internal WorldMapComponents(SimpleWorldMapScreen screen)
    {
        _screen = screen;
    }

    internal int TileSize { get; private set; } = DefaultTileSize;
    internal WorldMapFogManager? Fog { get; private set; }
    internal WorldMapTerrainRenderer? Terrain { get; private set; }
    internal WorldMapBiomeOverlay? BiomeOverlay { get; private set; }

    // Get tile registry for rendering and assign TileSet to layers
    internal void Initialize(ITileRegistry registry)
    {
        var usingCompiledAtlas = registry.UsingCompiledAtlas && registry.CompiledTileSet != null;
        TileSize = registry.TilesetConfig.BaseTileSize.X;

        ILog.Print($"[SimpleWorldMapScreen] TileRegistry: UsingCompiledAtlas={registry.UsingCompiledAtlas}, " +
                   $"TileSize={TileSize}");

        // Initialize components that need tile registry
        CreateComponents(registry, usingCompiledAtlas);

        // Assign TileSet to layers
        TileMapLayer?[] layers =
        [
            _screen.TerrainLayer, _screen.DecorationLayer, _screen.StructureLayer,
            _screen.EffectLayer, _screen.OverlayLayer, _screen.BiomeOverlayLayer
        ];
        if (usingCompiledAtlas)
        {
            LayerTileSets.Assign(registry.CompiledTileSet!, layers);
        }
        else
        {
            LayerTileSets.LoadAndAssign(registry.TilesetPath, layers);
        }
    }

    private void CreateComponents(ITileRegistry registry, bool usingCompiledAtlas)
    {
        var viewport = _screen.Viewport;
        if (viewport == null) return;

        Fog = new WorldMapFogManager(viewport, TileSize);
        BiomeOverlay = new WorldMapBiomeOverlay(viewport, TileSize);
        Terrain = new WorldMapTerrainRenderer(
            _screen.TerrainLayer, _screen.DecorationLayer, _screen.StructureLayer, _screen.EffectLayer,
            registry, usingCompiledAtlas);
    }
}
