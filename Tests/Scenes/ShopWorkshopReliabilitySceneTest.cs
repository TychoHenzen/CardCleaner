using System;
using System.Reflection;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// Reliability of the workshop cabinet: the shop scene hands the service locator exactly the singletons the
/// workshop owns, and the button returns to its dark state after a finished session so a second session can run.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopWorkshopReliabilitySceneTest
{
    private const int SmallMeshRings = 3;

    private static readonly CardSignature SomeCard = new() { Febris = 0.4f };

    private WorkshopSceneRig _rig = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _rig = await WorkshopSceneRig.Create();
        _rig.Screen.MeshRings = SmallMeshRings;
    }

    [AfterTest]
    public static void Teardown()
    {
        ServiceLocator.ResetForTesting();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task FinishedSessionLightsTheButtonForResetAndTheResetPressDarkensIt()
    {
        await RunSession();

        RaiseExplorationFinished(_rig.Screen);

        AssertBool(_rig.Button.Enabled).IsTrue();
        AssertBool(_rig.Button.ButtonMesh!.Visible).IsTrue();

        _rig.Button.Interact();
        await WorkshopSceneRig.Settle();

        AssertBool(_rig.Button.Enabled).IsFalse();
        AssertBool(_rig.Button.ButtonMesh!.Visible).IsFalse();
        AssertBool(_rig.Screen.IsInitialized).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task SecondSessionRunsAfterTheResetOnceBothHoldersAreFilledAgain()
    {
        await RunSession();
        RaiseExplorationFinished(_rig.Screen);
        _rig.Button.Interact();
        await WorkshopSceneRig.Settle();

        await _rig.Place(_rig.DeckHolder, SomeCard);
        AssertBool(_rig.Button.Enabled).IsFalse();

        await _rig.Place(_rig.CardHolder, SomeCard);
        AssertBool(_rig.Button.Enabled).IsTrue();

        _rig.Button.Interact();
        await WorkshopSceneRig.Settle();

        AssertBool(_rig.DeckHolder.HasCards).IsFalse();
        AssertBool(_rig.CardHolder.HasCards).IsFalse();
        AssertBool(_rig.Screen.IsInitialized).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task RealServiceRegistrationResolvesTheWorkshopSingletons()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var previousScene = tree.CurrentScene;
        var originalParent = _rig.Shop.GetParent();
        originalParent.RemoveChild(_rig.Shop);
        tree.Root.AddChild(_rig.Shop);
        tree.CurrentScene = _rig.Shop;

        try
        {
            ServiceLocator.ReinitializeServices();
            await WorkshopSceneRig.Settle();

            AssertBool(ReferenceEquals(ServiceLocator.Get<IGameSessionService>(),
                _rig.Workshop.GetNode("GameSessionService"))).IsTrue();
            AssertBool(ReferenceEquals(ServiceLocator.Get<ICardSpawningService>(),
                _rig.Shop.GetNode("Services/SpawningService"))).IsTrue();
            AssertBool(ServiceLocator.Has<IGameSettings>()).IsTrue();
        }
        finally
        {
            tree.CurrentScene = previousScene;
            tree.Root.RemoveChild(_rig.Shop);
            originalParent.AddChild(_rig.Shop);
        }
    }

    private async Task RunSession()
    {
        await _rig.Place(_rig.CardHolder, SomeCard);
        _rig.Button.Interact();
        await WorkshopSceneRig.Settle();
        AssertBool(_rig.Screen.IsInitialized).IsTrue();
    }

    private static void RaiseExplorationFinished(IrregularWorldMapScreen screen)
    {
        var backingField = typeof(IrregularWorldMapScreen).GetField(
            nameof(IrregularWorldMapScreen.ExplorationFinished),
            BindingFlags.Instance | BindingFlags.NonPublic);
        ((Action?)backingField!.GetValue(screen))?.Invoke();
    }
}
