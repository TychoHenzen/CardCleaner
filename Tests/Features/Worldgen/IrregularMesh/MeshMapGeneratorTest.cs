using CardCleaner.Scripts.Core.Services.CompiledAtlas;
using CardCleaner.Tests.TestUtilities;
using System;
using System.Threading;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh.WorldMap;
using IrregularMeshNs = CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

namespace CardCleaner.Tests.Features.Worldgen.IrregularMesh;

/// <summary>
/// The irregular map's IMapGenerator: the factory path's terrain, the shared placement rule, the same map for the
/// same seed and cards, cancellation, and generation off the main thread.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class MeshMapGeneratorTest
{
    private const int Seed = 4242;
    private const int SmallRings = 3;
    private const int PlacementRings = 6;

    [TestCase(Timeout = 120000)]
    [TestCategory("Unit")]
    public static async Task GeneratesAMeshMapWithAStartAndEnemiesFromTheSharedRule()
    {
        var generated = await MeshMapGeneratorFixture.Create(PlacementRings).GenerateAsync(Config());

        var meshMap = generated as IrregularMeshNs.IMeshGeneratedMap;
        AssertThat(meshMap).IsNotNull();
        AssertThat(meshMap!.WorldScale).IsEqual(IrregularMeshNs.MeshMapGenerator.WorldScale);
        var map = meshMap.MeshMapData;
        AssertThat(map.PlayerStartCell).IsNotNull();
        AssertBool(map.IsPassable(map.PlayerStartCell!.Value)).IsTrue();
        AssertThat(generated.EnemyCount).IsBetween(MapPopulation.MinEnemies, MapPopulation.MaxEnemies);
        foreach (var enemy in map.EnemySpawnCells)
        {
            AssertBool(map.IsPassable(enemy)).IsTrue();
            AssertThat(enemy).IsNotEqual(map.PlayerStartCell.Value);
        }
    }

    [TestCase(Timeout = 120000)]
    [TestCategory("Unit")]
    public static async Task TerrainIsTheTerrainOfTheFactoryPath()
    {
        var registry = TileCatalogLoad.Load(new GodotAssetPaths(), new CompiledAtlasSource());
        var biomes = MeshMapGeneratorFixture.DefaultBiomes();
        var cards = MeshMapGeneratorFixture.TerrainCards();
        var expected = TerrainGeneratorFactory
            .Create(TileCatalogData.From(registry), biomes, CompiledTransitionPairs.Load())
            .GenerateWithSampler(
                SmallRings, TerrainGeneratorFactory.CardSampler(cards, Seed), Seed, relaxationIterations: 8);

        var generator = new IrregularMeshNs.MeshMapGenerator(
            TileCatalogData.From(registry), biomes, CompiledTransitionPairs.Load(), SmallRings);
        var generated = (IrregularMeshNs.IMeshGeneratedMap)await generator.GenerateAsync(Config());

        var expectedTerrain = MeshMapGeneratorFixture.TerrainOf(expected);
        AssertThat(MeshMapGeneratorFixture.TerrainOf(generated.RawMesh)).IsEqual(expectedTerrain);
    }

    [TestCase(Timeout = 120000)]
    [TestCategory("Unit")]
    public static async Task TheSameSeedAndCardsGiveTheSameMap()
    {
        var generator = MeshMapGeneratorFixture.Create(SmallRings);

        var first = (IrregularMeshNs.IMeshGeneratedMap)await generator.GenerateAsync(Config());
        var second = (IrregularMeshNs.IMeshGeneratedMap)await generator.GenerateAsync(Config());

        AssertThat(Describe(second)).IsEqual(Describe(first));
    }

    [TestCase(Timeout = 120000)]
    [TestCategory("Unit")]
    public static async Task ACancelledTokenThrowsMapGenerationCancelled()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var thrown = false;

        try
        {
            await MeshMapGeneratorFixture.Create(SmallRings).GenerateAsync(Config(), null, cancellation.Token);
        }
        catch (MapGenerationCancelledException)
        {
            thrown = true;
        }

        AssertBool(thrown).IsTrue();
    }

    [TestCase(Timeout = 120000)]
    [TestCategory("Unit")]
    public static async Task GenerationRunsOffTheMainThreadAndReportsCompletion()
    {
        var mainThread = Environment.CurrentManagedThreadId;
        var progress = new ThreadRecordingProgress();

        await MeshMapGeneratorFixture.Create(SmallRings).GenerateAsync(Config(), progress);

        AssertThat(progress.LastValue).IsEqual(1f);
        AssertThat(progress.LastThread).IsNotEqual(mainThread);
    }

    private static MapGenerationConfig Config() => new()
    {
        Seed = Seed,
        MapSeeds = MeshMapGeneratorFixture.TerrainCards()
    };

    private static string Describe(IrregularMeshNs.IMeshGeneratedMap map) =>
        $"{MeshMapGeneratorFixture.TerrainOf(map.RawMesh)}|start {map.MeshMapData.PlayerStartCell}|enemies " +
        string.Join(";", map.MeshMapData.EnemySpawnCells);

    // Records the thread of each report; IProgress<T> implementations other than Progress<T> report inline.
    private sealed class ThreadRecordingProgress : IProgress<float>
    {
        public float LastValue { get; private set; } = -1f;

        public int LastThread { get; private set; } = -1;

        public void Report(float value)
        {
            LastValue = value;
            LastThread = Environment.CurrentManagedThreadId;
        }
    }
}
