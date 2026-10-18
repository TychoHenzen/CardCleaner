using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Tests.TestUtilities;
using Godot;
using IrregularMeshNs = CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

namespace CardCleaner.Tests.Features.Worldgen.IrregularMesh;

/// <summary>
/// Enemy sprites must never be drawn above fog that still covers their cell. The session is reset once the map
/// shows, so each test sets the fog itself through the visibility the session would hand it.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class IrregularWorldMapScreenEnemyFogTest
{
    private const int EnemyZIndex = 80;
    private const int MapFrameBudget = 300;

    private static readonly IrregularMeshNs.MeshMapGenerator Generator = MeshMapGeneratorFixture.Create(4);

    private static readonly CardSignature[] Abilities =
    {
        new(new[] { 0.5f, -0.3f, 0.8f, -0.1f, 0.2f, -0.7f, 0.9f, -0.4f })
    };

    private IrregularMeshNs.IrregularWorldMapScreen _screen = null!;

    [BeforeTest]
    public async Task Setup()
    {
        TestServices.Reset();
        TestServices.Container.RegisterSingleton<ITileRegistry>(new TileRegistry(new GodotAssetPaths()));
        var session = TestServices.CreateGameSession();
        TestServices.Container.RegisterSingleton<IGameSessionService>(session);
        AddNode(session);

        var viewport = new SubViewport { Name = "Viewport" };
        _screen = new IrregularMeshNs.IrregularWorldMapScreen { Viewport = viewport, FogOfWarEnabled = true };
        _screen.AddChild(viewport);
        AddNode(_screen);
        await ISceneRunner.SyncProcessFrame;

        _screen.Initialize(MeshMapGeneratorFixture.TerrainCards(), Abilities, Generator);
        for (var frame = 0; frame < MapFrameBudget && !_screen.IsInitialized; frame++)
            await ISceneRunner.SyncProcessFrame;

        session.ResetSession();
        AssertBool(_screen.IsInitialized).IsTrue();
        _screen.FogOfWar!.Reset();
    }

    [AfterTest]
    public static void Teardown() => TestServices.Reset();

    private Sprite2D[] EnemySprites() =>
        _screen.GetNode("Viewport").GetChildren().OfType<Sprite2D>().Where(s => s.ZIndex == EnemyZIndex).ToArray();

    private int CellOf(Sprite2D sprite) =>
        Enumerable.Range(0, _screen.MapData!.CellCount).First(i => _screen.MapData.GetCellCenter(i) == sprite.Position);

    private static HashSet<int> Cells(params int[] cellIds) => new(cellIds);

    [TestCase]
    [TestCategory("Unit")]
    public void EnemiesInHiddenCellsAreNotShownAboveTheFog()
    {
        AssertThat(EnemySprites().Length).IsGreater(0);
        AssertThat(EnemySprites().Count(s => s.Visible)).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void AnEnemyShowsWhileItsCellIsVisible()
    {
        var enemy = EnemySprites()[0];
        var cell = CellOf(enemy);

        _screen.FogOfWar!.UpdateVisibility(Cells(cell), Cells(cell));

        AssertBool(enemy.Visible).IsTrue();
        AssertThat(EnemySprites().Count(s => s.Visible)).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void EnemiesBecomeVisibleWhenTheMapIsRevealed()
    {
        _screen.RevealAll();

        AssertThat(EnemySprites().Count(s => !s.Visible)).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void AnEnemyHidesAgainOnceItsCellIsOnlySeen()
    {
        var enemy = EnemySprites()[0];
        var cell = CellOf(enemy);
        _screen.FogOfWar!.UpdateVisibility(Cells(cell), Cells(cell));

        _screen.FogOfWar.UpdateVisibility(Cells(cell), Cells());

        AssertThat(_screen.FogOfWar.GetFogState(cell)).IsEqual(FogState.Revealed);
        AssertBool(enemy.Visible).IsFalse();
    }
}
