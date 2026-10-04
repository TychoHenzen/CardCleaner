using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using IrregularMeshNs = CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

namespace CardCleaner.Tests.Features.Worldgen.IrregularMesh;

/// <summary>
/// Enemy sprites must never be drawn above fog that still covers their cell.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class IrregularWorldMapScreenEnemyFogTest
{
    private const int EnemyZIndex = 80;
    private const int Seed = 4242;

    private readonly List<IrregularMeshNs.IrregularWorldMapScreen> _screens = new();

    private IrregularMeshNs.IrregularWorldMapScreen CreateScreen()
    {
        var viewport = new SubViewport { Name = "Viewport" };
        var screen = new IrregularMeshNs.IrregularWorldMapScreen
        {
            MeshRings = 4,
            WorldScale = 16f,
            VisionRange = 3f,
            FogOfWarEnabled = true,
            ShowDebug = false,
            Viewport = viewport
        };
        screen.AddChild(viewport);
        _screens.Add(screen);
        return screen;
    }

    private static Sprite2D[] EnemySprites(Node screen) =>
        screen.GetNode("Viewport").GetChildren().OfType<Sprite2D>().Where(s => s.ZIndex == EnemyZIndex).ToArray();

    private static int CellOf(IrregularMeshNs.IrregularWorldMapScreen screen, Sprite2D sprite) =>
        Enumerable.Range(0, screen.MapData!.CellCount).First(i => screen.MapData.GetCellCenter(i) == sprite.Position);

    [TestCase]
    public async Task EnemiesInHiddenCellsAreNotShownAboveTheFog()
    {
        var screen = CreateScreen();
        AddNode(screen);
        await ISceneRunner.SyncProcessFrame;
        screen.GenerateMap(Seed);

        var hiddenEnemies = EnemySprites(screen).Where(s => !screen.FogOfWar!.IsVisible(CellOf(screen, s))).ToArray();

        AssertThat(EnemySprites(screen).Length).IsGreater(0);
        AssertThat(hiddenEnemies.Length).IsGreater(0);
        AssertThat(hiddenEnemies.Count(s => s.Visible)).IsEqual(0);
    }

    [TestCase]
    public async Task EnemiesBecomeVisibleWhenTheirCellIsRevealed()
    {
        var screen = CreateScreen();
        AddNode(screen);
        await ISceneRunner.SyncProcessFrame;
        screen.GenerateMap(Seed);

        screen.RevealAll();

        AssertThat(EnemySprites(screen).Count(s => !s.Visible)).IsEqual(0);
    }

    [TestCase]
    public async Task EnemiesHideAgainOnceTheirCellIsOnlyRevealed()
    {
        var screen = CreateScreen();
        AddNode(screen);
        await ISceneRunner.SyncProcessFrame;
        screen.GenerateMap(Seed);
        screen.RevealAll();

        screen.FogOfWar!.VisionRange = 0.1f;
        screen.FogOfWar.UpdateVisibility(0);

        var offscreenEnemies = EnemySprites(screen).Where(s => CellOf(screen, s) != 0).ToArray();
        AssertThat(offscreenEnemies.Length).IsGreater(0);
        AssertThat(offscreenEnemies.Count(s => s.Visible)).IsEqual(0);
    }
}
