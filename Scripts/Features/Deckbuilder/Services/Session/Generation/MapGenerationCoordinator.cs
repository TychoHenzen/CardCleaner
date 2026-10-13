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
    private readonly SessionWorld _world;
    private readonly GridCellEventAdapter _eventAdapter;
    private readonly GenerationTracker _tracker;

    internal event Action<SimpleMapData>? MapGenerated;
    internal event Action<IGeneratedMap>? GeneratedMapReady;
    internal event Action<float>? ProgressUpdated;

    /// <summary>Raised with the generation id when a still-current generation completed.</summary>
    internal event Action<long>? Succeeded;

    /// <summary>Raised when a still-current generation was cancelled or failed.</summary>
    internal event Action? Abandoned;

    internal MapGenerationCoordinator(
        Node owner,
        SessionWorld world,
        GridCellEventAdapter eventAdapter,
        GenerationTracker tracker)
    {
        _owner = owner;
        _world = world;
        _eventAdapter = eventAdapter;
        _tracker = tracker;
    }

    internal bool IsCurrent(long generationId)
    {
        return GodotObject.IsInstanceValid(_owner) && _tracker.IsCurrent(generationId);
    }

    internal async Task GenerateAsync(CardSignature[] mapSeeds, IMapGenerator generator)
    {
        var run = _tracker.BeginRun();
        ILog.Print($"Starting async map generation from {mapSeeds.Length} seed signature(s)...");

        // Create progress reporter for loading UI
        var progress = new GenerationProgressBinding(
            _owner, value => OnProgress(run.GenerationId, value), run.Cts.Token);

        try
        {
            var request = CreateRequest(run, mapSeeds, progress.Progress);

            await GenerateWith(request, generator);

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

    private async Task GenerateWith(MapGenerationRequest request, IMapGenerator generator)
    {
        var config = new MapGenerationConfig
        {
            Size = request.MapSize,
            Seed = request.Seed,
            MapSeeds = request.MapSeeds
        };

        var generatedMap = await generator.GenerateAsync(config, request.Progress, request.Run.Cts.Token);
        if (!IsCurrentRun(request.Run))
            return;

        // ASSUMPTION: combat and loot draw from a fresh RNG seeded like the map. The grid generator builds its own
        // RNG from the same seed, so a grid session's combat and loot draws differ from the ones before #284, where
        // they continued the RNG the map generation had used; the map itself is unchanged.
        _world.Rng = new RandomNumberGenerator { Seed = request.Seed };
        _world.GeneratedMap = generatedMap;
        _world.MapData = generatedMap.GetMapData();

        // A grid map also publishes its grid coordinates, so exploration raises grid events next to cell events.
        var gridMap = generatedMap as SimpleGeneratedMap;
        _world.GridMapData = gridMap?.GridMapData;
        _eventAdapter.SetGridMapData(_world.GridMapData);

        ILog.Print($"Map generated by {generator.GetType().Name}: {generatedMap.EnemyCount} enemies");

        if (gridMap != null)
        {
            MapGenerated?.Invoke(gridMap.RawMapData);
            if (!IsCurrentRun(request.Run))
                return;
        }

        GeneratedMapReady?.Invoke(generatedMap);
    }
}
