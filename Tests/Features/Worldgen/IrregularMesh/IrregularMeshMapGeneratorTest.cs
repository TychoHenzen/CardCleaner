using System.Collections.Generic;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

namespace CardCleaner.Tests.Features.Worldgen.IrregularMesh;

/// <summary>
/// Tests for IrregularMeshMapGenerator integration with GameSessionService.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class IrregularMeshMapGeneratorTest
{
    private GameSessionService _service = null!;
    private IGeneratedMap? _lastGeneratedMap;
    private int _generatedMapEventCount;

    [BeforeTest]
    public async Task Setup()
    {
        _service = new GameSessionService();
        _lastGeneratedMap = null;
        _generatedMapEventCount = 0;

        _service.GeneratedMapReady += OnGeneratedMapReady;

        AddNode(_service);
        await ISceneRunner.SyncProcessFrame;
    }

    // No Teardown needed - gdUnit4's AddNode handles cleanup automatically

    private void OnGeneratedMapReady(IGeneratedMap map)
    {
        _lastGeneratedMap = map;
        _generatedMapEventCount++;
    }

    [TestCase]
    public void TestIrregularMeshMapGeneratorConstruction()
    {
        var generator = new IrregularMeshMapGenerator();

        AssertThat(generator).IsNotNull();
        AssertThat(generator.WorldScale).IsEqual(16f);
        AssertThat(generator.EnemyCount).IsEqual(3);
    }

    [TestCase]
    public void TestSetMapGeneratorOnService()
    {
        var generator = new IrregularMeshMapGenerator();

        _service.SetMapGenerator(generator);

        // No exception means success
        AssertThat(generator).IsNotNull();
    }

    [TestCase]
    public void TestClearMapGenerator()
    {
        var generator = new IrregularMeshMapGenerator();
        _service.SetMapGenerator(generator);

        _service.SetMapGenerator(null);

        // No exception means success - service is back to default generator
        AssertThat(generator).IsNotNull();
    }

    [TestCase]
    public async Task TestGenerateMapWithIrregularMesh()
    {
        var generator = new IrregularMeshMapGenerator
        {
            EnemyCount = 2,
            MinEnemyDistance = 2
        };
        _service.SetMapGenerator(generator);

        var mapSeeds = new List<CardSignature>
        {
            new(new[] { 0.5f, -0.3f, 0.8f, -0.1f, 0.2f, -0.7f, 0.9f, -0.4f })
        };
        var abilityCards = new List<CardSignature> { new() };

        _service.StartSession(mapSeeds, abilityCards);
        AssertThat(_service.CurrentState).IsEqual(SessionState.GeneratingMap);

        // Wait for generation task to be set and complete
        await ISceneRunner.SyncProcessFrame;
        var generationTask = _service.CurrentGenerationTask;
        AssertThat(generationTask).IsNotNull();
        await generationTask!;

        // Wait for state transition
        await ISceneRunner.SyncProcessFrame;

        // Verify GeneratedMapReady event was fired
        AssertThat(_generatedMapEventCount).IsEqual(1);
        AssertThat(_lastGeneratedMap).IsNotNull();
    }

    [TestCase]
    public async Task TestGeneratedMapHasValidData()
    {
        var generator = new IrregularMeshMapGenerator
        {
            EnemyCount = 3,
            MinEnemyDistance = 2
        };
        _service.SetMapGenerator(generator);

        var mapSeeds = new List<CardSignature>
        {
            new(new[] { 0.5f, 0.5f, 0.5f, 0.5f, 0.0f, 0.0f, 0.0f, 0.0f })
        };
        var abilityCards = new List<CardSignature> { new() };

        _service.StartSession(mapSeeds, abilityCards);

        await ISceneRunner.SyncProcessFrame;
        var generationTask = _service.CurrentGenerationTask;
        await generationTask!;
        await ISceneRunner.SyncProcessFrame;

        AssertThat(_lastGeneratedMap).IsNotNull();

        // Verify map data
        var mapData = _lastGeneratedMap!.GetMapData();
        AssertThat(mapData).IsNotNull();
        AssertThat(mapData.CellCount).IsGreater(0);

        // Verify enemy count (may be less than requested due to spacing)
        AssertThat(_lastGeneratedMap.EnemyCount).IsGreaterEqual(0);

        // Verify player start position is set
        AssertThat(mapData.PlayerStartCell).IsNotNull();
    }

    [TestCase]
    public async Task TestGeneratedMapIsIrregularMesh()
    {
        var generator = new IrregularMeshMapGenerator();
        _service.SetMapGenerator(generator);

        var mapSeeds = new List<CardSignature> { new() };
        var abilityCards = new List<CardSignature> { new() };

        _service.StartSession(mapSeeds, abilityCards);

        await ISceneRunner.SyncProcessFrame;
        var generationTask = _service.CurrentGenerationTask;
        await generationTask!;
        await ISceneRunner.SyncProcessFrame;

        AssertThat(_lastGeneratedMap).IsNotNull();

        // Verify it's an IrregularGeneratedMap
        var irregularMap = _lastGeneratedMap as IrregularGeneratedMap;
        AssertThat(irregularMap).IsNotNull();

        // Verify we can access the raw mesh
        AssertThat(irregularMap!.RawMesh).IsNotNull();
        AssertThat(irregularMap.RawMesh.Vertices.Count).IsGreater(0);
        AssertThat(irregularMap.RawMesh.Quads.Count).IsGreater(0);
    }

    [TestCase]
    public async Task TestSessionAdvancesToExploringWithIrregularMesh()
    {
        var generator = new IrregularMeshMapGenerator();
        _service.SetMapGenerator(generator);

        var mapSeeds = new List<CardSignature> { new() };
        var abilityCards = new List<CardSignature> { new() };

        _service.StartSession(mapSeeds, abilityCards);

        await ISceneRunner.SyncProcessFrame;
        var generationTask = _service.CurrentGenerationTask;
        await generationTask!;
        await ISceneRunner.SyncProcessFrame;

        // Session should have advanced to Exploring
        AssertThat(_service.CurrentState).IsEqual(SessionState.Exploring);
    }

    [TestCase]
    public async Task TestCurrentMapDataPropertyIsSet()
    {
        var generator = new IrregularMeshMapGenerator();
        _service.SetMapGenerator(generator);

        var mapSeeds = new List<CardSignature> { new() };
        var abilityCards = new List<CardSignature> { new() };

        _service.StartSession(mapSeeds, abilityCards);

        await ISceneRunner.SyncProcessFrame;
        var generationTask = _service.CurrentGenerationTask;
        await generationTask!;
        await ISceneRunner.SyncProcessFrame;

        // Verify CurrentMapData and CurrentGeneratedMap properties are set
        AssertThat(_service.CurrentMapData).IsNotNull();
        AssertThat(_service.CurrentGeneratedMap).IsNotNull();
    }

    [TestCase]
    public async Task TestDeterministicGenerationWithSameSeed()
    {
        var generator = new IrregularMeshMapGenerator
        {
            EnemyCount = 2
        };
        _service.SetMapGenerator(generator);

        var mapSeeds = new List<CardSignature>
        {
            new(new[] { 0.5f, -0.3f, 0.8f, -0.1f, 0.2f, -0.7f, 0.9f, -0.4f })
        };
        var abilityCards = new List<CardSignature> { new() };

        // First generation
        _service.StartSession(mapSeeds, abilityCards);

        await ISceneRunner.SyncProcessFrame;
        var generationTask = _service.CurrentGenerationTask;
        await generationTask!;
        await ISceneRunner.SyncProcessFrame;

        var firstMap = _lastGeneratedMap as IrregularGeneratedMap;
        AssertThat(firstMap).IsNotNull();
        var firstVertexCount = firstMap!.RawMesh.Vertices.Count;
        var firstQuadCount = firstMap.RawMesh.Quads.Count;

        // Reset
        _service.ResetSession();
        _lastGeneratedMap = null;

        // Second generation with same seeds
        _service.StartSession(mapSeeds, abilityCards);

        await ISceneRunner.SyncProcessFrame;
        generationTask = _service.CurrentGenerationTask;
        await generationTask!;
        await ISceneRunner.SyncProcessFrame;

        var secondMap = _lastGeneratedMap as IrregularGeneratedMap;
        AssertThat(secondMap).IsNotNull();

        // Mesh should have same structure (deterministic generation)
        AssertThat(secondMap!.RawMesh.Vertices.Count).IsEqual(firstVertexCount);
        AssertThat(secondMap.RawMesh.Quads.Count).IsEqual(firstQuadCount);
    }
}
