using System;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services.Session.Generation;

/// <summary>
/// Runs one map generation attempt, publishes the resulting map into the session world,
/// and reports whether the attempt completed or must be abandoned.
/// </summary>
internal sealed class MapGenerationCoordinator
{
    private readonly Node _owner;
    private readonly SessionServices _services;
    private readonly SessionWorld _world;
    private readonly GridCellEventAdapter _eventAdapter;
    private readonly GenerationTracker _tracker;
    private readonly DefaultSessionMapBuilder _defaultBuilder;

    internal event Action<SimpleMapData>? MapGenerated;
    internal event Action<IGeneratedMap>? GeneratedMapReady;
    internal event Action<float>? ProgressUpdated;

    /// <summary>Raised with the generation id when a still-current generation completed.</summary>
    internal event Action<long>? Succeeded;

    /// <summary>Raised when a still-current generation was cancelled or failed.</summary>
    internal event Action? Abandoned;

    internal MapGenerationCoordinator(
        Node owner,
        SessionServices services,
        SessionWorld world,
        GridCellEventAdapter eventAdapter,
        GenerationTracker tracker)
    {
        _owner = owner;
        _services = services;
        _world = world;
        _eventAdapter = eventAdapter;
        _tracker = tracker;
        _defaultBuilder = new DefaultSessionMapBuilder(services);
    }

    internal bool IsCurrent(long generationId)
    {
        return GodotObject.IsInstanceValid(_owner) && _tracker.IsCurrent(generationId);
    }

    internal async Task GenerateAsync(CardSignature[] mapSeeds, IMapGenerator? customGenerator)
    {
        var run = _tracker.BeginRun();
        ILog.Print($"Starting async map generation from {mapSeeds.Length} seed signature(s)...");

        // Create progress reporter for loading UI
        var progress = new GenerationProgressBinding(
            _owner, value => OnProgress(run.GenerationId, value), run.Cts.Token);

        try
        {
            var request = CreateRequest(run, mapSeeds, progress.Progress);

            await (customGenerator != null
                ? GenerateWithCustomGenerator(request, customGenerator)
                : GenerateWithDefaultGenerator(request));

            if (!IsCurrentRun(run))
                return;

            Succeeded?.Invoke(run.GenerationId);
        }
        catch (OperationCanceledException ex)
        {
            ILog.Warning($"Map generation cancelled: {ex.Message}");
            AbandonIfCurrent(run);
        }
        catch (Exception ex)
        {
            ILog.Error($"Map generation failed: {ex.Message}");
            ILog.Error($"Stack trace: {ex.StackTrace}");
            AbandonIfCurrent(run);
        }
        finally
        {
            progress.Detach();
            _tracker.Release(run.Cts);
        }
    }

    private static MapGenerationRequest CreateRequest(
        GenerationRun run,
        CardSignature[] mapSeeds,
        IProgress<float> progress)
    {
        // Compute deterministic seed from card signatures
        var seed = MapSeeding.ComputeSeedFromCards(mapSeeds);
        ILog.Print($"Map seed: {seed}");

        return new MapGenerationRequest
        {
            Run = run,
            Rng = new RandomNumberGenerator { Seed = seed },
            Seed = seed,
            // Use first signature to influence map size (could blend in future)
            MapSize = MapSeeding.CalculateMapSize(mapSeeds[0]),
            MapSeeds = mapSeeds,
            Progress = progress
        };
    }

    private bool IsCurrentRun(GenerationRun run)
    {
        return GodotObject.IsInstanceValid(_owner) && _tracker.IsCurrent(run.GenerationId, run.Cts);
    }

    private void AbandonIfCurrent(GenerationRun run)
    {
        if (IsCurrentRun(run))
            Abandoned?.Invoke();
    }

    private void OnProgress(long generationId, float value)
    {
        if (!IsCurrent(generationId))
            return;

        ProgressUpdated?.Invoke(value);
        if (!IsCurrent(generationId))
            return;

        ILog.Print($"Map generation: {value * 100:F0}%");
    }

    private async Task GenerateWithCustomGenerator(MapGenerationRequest request, IMapGenerator mapGenerator)
    {
        // Create generation config from current session state
        var config = new MapGenerationConfig
        {
            Size = request.MapSize,
            Seed = request.Seed,
            MapSeeds = request.MapSeeds,
            BiomeRegistry = _services.BiomeRegistry
        };

        // Generate using the custom generator
        var generatedMap = await mapGenerator.GenerateAsync(config, request.Progress, request.Run.Cts.Token);
        if (!IsCurrentRun(request.Run))
            return;

        _world.Rng = request.Rng;
        _world.GeneratedMap = generatedMap;
        _world.MapData = generatedMap.GetMapData();

        // Clear regular grid fields (not used with custom generator)
        _world.LegacyMap = null;
        _world.GridMapData = null;
        _eventAdapter.SetGridMapData(null);

        ILog.Print($"Custom map generated: {generatedMap.EnemyCount} enemies");

        if (generatedMap is SimpleGeneratedMap simpleMap)
            MapGenerated?.Invoke(simpleMap.RawMapData);

        // Notify listeners with the new event
        GeneratedMapReady?.Invoke(generatedMap);
    }

    private async Task GenerateWithDefaultGenerator(MapGenerationRequest request)
    {
        var built = await _defaultBuilder.BuildAsync(request);
        if (!IsCurrentRun(request.Run))
            return;

        var map = built.Map;
        _world.Rng = request.Rng;
        _world.LegacyMap = map;

        // Create the IMapData adapter for exploration
        _world.GridMapData = new RegularGridMapData(map);
        _world.MapData = _world.GridMapData;
        _eventAdapter.SetGridMapData(_world.GridMapData);

        // Wrap in IGeneratedMap for unified interface
        var generatedMap = new SimpleGeneratedMap(map);
        _world.GeneratedMap = generatedMap;

        // Log biome distribution for debugging
        built.BiomeProvider.LogBiomeStats();

        ILog.Print($"Map generated: {request.MapSize.X}x{request.MapSize.Y}, {map.EnemyPositions.Count} enemies");

        // Notify listeners about the generated map (backward compatible event)
        MapGenerated?.Invoke(map);
        if (!IsCurrentRun(request.Run))
            return;

        GeneratedMapReady?.Invoke(generatedMap);
    }
}
