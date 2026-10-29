using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Components;
using CardCleaner.Scripts.Features.Deckbuilder.Components.WorldMapTerrainRendererSupport;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
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
        var tileSet = LoadCompiledTileSet(registry);
        var usingCompiledAtlas = tileSet != null;
        TileSize = registry.Visuals.TilesetConfig.BaseTileSize.X;

        ILog.Print(
            $"[SimpleWorldMapScreen] TileRegistry: " +
            $"UsingCompiledAtlas={registry.Visuals.UsingCompiledAtlas}, " +
            $"TileSize={TileSize}");

        // Initialize components that need tile registry
        CreateComponents(registry, usingCompiledAtlas);

        // Assign TileSet to layers
        TileMapLayer?[] layers =
        [
            _screen.TerrainLayer, _screen.DecorationLayer, _screen.StructureLayer,
            _screen.EffectLayer, _screen.OverlayLayer, _screen.BiomeOverlayLayer
        ];
        AssignTileSet(tileSet, registry.Visuals.TilesetPath, layers);
    }

    // The registry only decides the mode; the UI-layer loader owns the compiled TileSet.
    private static TileSet? LoadCompiledTileSet(ITileRegistry registry)
    {
        if (!registry.Visuals.UsingCompiledAtlas) return null;

        var tileSet = CompiledAtlasLoader.LoadCompiledTileSet();
        if (tileSet == null)
            ILog.Error("[SimpleWorldMapScreen] Compiled atlas registry but no compiled TileSet");
        return tileSet;
    }

    private static void AssignTileSet(TileSet? compiled, string tilesetPath, TileMapLayer?[] layers)
    {
        if (compiled == null)
        {
            LayerTileSets.LoadAndAssign(tilesetPath, layers);
            return;
        }

        LayerTileSets.Assign(compiled, layers);
    }

    private void CreateComponents(ITileRegistry registry, bool usingCompiledAtlas)
    {
        var viewport = _screen.Viewport;
        if (viewport == null) return;

        Fog = new WorldMapFogManager(viewport, TileSize);
        BiomeOverlay = new WorldMapBiomeOverlay(viewport, TileSize);
        var transitionResolver = usingCompiledAtlas ? TransitionResolvers.Create(new GodotAssetPaths()) : null;
        var atlasMapping = usingCompiledAtlas ? CompiledAtlasLoader.LoadMapping() : null;
        Terrain = new WorldMapTerrainRenderer(
            _screen.TerrainLayer, _screen.DecorationLayer, _screen.StructureLayer, _screen.EffectLayer,
            registry,
            TerrainTransitions.Create(transitionResolver, atlasMapping));
    }
}
