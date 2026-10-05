using System.Threading.Tasks;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// Reliability of the workshop cabinet: the button returns to its dark state after a finished session so a second session can run.
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

        _rig.Screen.RaiseExplorationFinished();

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
        _rig.Screen.RaiseExplorationFinished();
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

    private async Task RunSession()
    {
        await _rig.Place(_rig.CardHolder, SomeCard);
        _rig.Button.Interact();
        await WorkshopSceneRig.Settle();
        AssertBool(_rig.Screen.IsInitialized).IsTrue();
    }
}
