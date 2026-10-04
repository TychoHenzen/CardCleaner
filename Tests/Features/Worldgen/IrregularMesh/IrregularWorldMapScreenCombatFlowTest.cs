using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;
using IrregularMeshNs = CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

namespace CardCleaner.Tests.Features.Worldgen.IrregularMesh;

/// <summary>
/// Exploration hand-off to combat on IrregularWorldMapScreen: meeting an enemy starts combat instead of ending
/// the run, and every generation entry point can fight.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class IrregularWorldMapScreenCombatFlowTest
{
    private const int Seed = 12345;
    private const double EncounterTimeoutSeconds = 30;

    private IrregularMeshNs.IrregularWorldMapScreen _screen = null!;

    [BeforeTest]
    public async Task Setup()
    {
        var viewport = new SubViewport { Name = "Viewport" };
        _screen = new IrregularMeshNs.IrregularWorldMapScreen
        {
            MeshRings = 3,
            MovementSpeed = 0.01f,
            FogOfWarEnabled = true,
            Viewport = viewport
        };
        _screen.AddChild(viewport);
        AddNode(_screen);
        await ISceneRunner.SyncProcessFrame;
    }

    private static CardSignature StrongAbility() => new(new[] { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f });

    private static async Task<bool> WaitUntil(Func<bool> condition, double timeoutSeconds)
    {
        var deadline = Time.GetTicksMsec() + (ulong)(timeoutSeconds * 1000);
        while (!condition() && Time.GetTicksMsec() < deadline)
            await ISceneRunner.SyncProcessFrame;

        return condition();
    }

    [TestCase]
    public async Task MeetingAnEnemyDoesNotFinishExploration()
    {
        var encountered = false;
        var finishedCount = 0;
        _screen.EnemyEncountered += _ => encountered = true;
        _screen.ExplorationFinished += () => finishedCount++;

        _screen.GenerateMap(Seed);

        AssertBool(await WaitUntil(() => encountered, EncounterTimeoutSeconds)).IsTrue();
        for (var frame = 0; frame < 30; frame++)
            await ISceneRunner.SyncProcessFrame;

        AssertThat(finishedCount).IsEqual(0);
    }

    [TestCase]
    public async Task WfcGeneratedMapFightsEnemiesWithTheGivenAbilities()
    {
        var adjacency = new Dictionary<string, HashSet<string>>
        {
            { "grass", new HashSet<string> { "grass", "sand" } },
            { "sand", new HashSet<string> { "grass", "sand" } }
        };
        var tileToTerrain = new Dictionary<string, int> { { "grass", 1 }, { "sand", 2 } };
        var defeated = false;
        _screen.EnemyDefeated += _ => defeated = true;

        _screen.GenerateMapWithWfc(Seed, adjacency, tileToTerrain, new[] { StrongAbility() });

        AssertBool(await WaitUntil(() => defeated, EncounterTimeoutSeconds)).IsTrue();
    }
}
