using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.WorldMap;

/// <summary>
/// The view built for one generated map: map data, enemy sprites, fog of war, renderers and the player's movement
/// animation. It explores and fights nothing; the screen feeds it what the game session reports.
/// </summary>
internal sealed class WorldMapSession
{
    private readonly WorldMapSettings _settings;
    private readonly WorldMapRenderLayer _renderLayer;

    private IrregularMapEnemyManager? _enemyManager;

    internal WorldMapSession(WorldMapSettings settings, IMeshGeneratedMap map)
    {
        _settings = settings;
        MapData = map.MeshMapData;
        _renderLayer = new WorldMapRenderLayer(settings, map.RawMesh, MapData);
    }

    internal IMapData MapData { get; }

    internal IrregularMeshFogOfWar? FogOfWar { get; private set; }

    /// <summary>The player's movement animation; null when the screen has no viewport.</summary>
    internal IrregularMeshMovementController? Movement { get; private set; }

    /// <summary>
    /// Creates the enemy sprites, the fog of war, the renderers and the movement animation at the start the generator
    /// placed. Returns the player start cell.
    /// </summary>
    internal int Build()
    {
        var playerStartCell = MapData.PlayerStartCell ?? 0;
        var viewport = _settings.Viewport;
        if (viewport != null)
        {
            _enemyManager = new IrregularMapEnemyManager(MapData, viewport);
            _enemyManager.CreateEnemySprites();

            Movement = new IrregularMeshMovementController { Name = "MovementController" };
            viewport.AddChild(Movement);
            Movement.Initialize(MapData, playerStartCell);
        }

        if (_settings.FogOfWarEnabled)
        {
            FogOfWar = new IrregularMeshFogOfWar(MapData);
            _enemyManager?.BindFog(FogOfWar);
        }

        _renderLayer.Build(FogOfWar);
        return playerStartCell;
    }

    internal void RevealAll() => FogOfWar?.RevealAll();

    internal void RemoveEnemyAt(int cellId) => _enemyManager?.RemoveEnemyAt(cellId);

    /// <summary>
    /// Resets the fog of war and frees the renderers, the enemy sprites and the movement animation.
    /// </summary>
    internal void Close()
    {
        FogOfWar?.Reset();
        _renderLayer.Clear();
        _enemyManager?.ClearSprites();

        if (Movement != null && GodotObject.IsInstanceValid(Movement))
        {
            Movement.CancelMovement();
            Movement.QueueFree();
        }

        Movement = null;
    }
}
