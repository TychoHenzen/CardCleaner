using System.Threading.Tasks;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Features.Deckbuilder.Controllers;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// Reliability of the workshop cabinet: the button returns to its dark state after a finished session so a second session can run.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopWorkshopReliabilitySceneTest
{
    private static readonly CardSignature SomeCard = new CardSignature().With(Element.Febris, 0.4f);

    private WorkshopSceneRig _rig = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _rig = await WorkshopSceneRig.Create();
        _rig.PickKind(MapGenerationType.IrregularMesh);
    }

    [AfterTest]
    public static void Teardown()
    {
        Engine.TimeScale = 1;
        ServiceLocator.ResetForTesting();
    }

    [TestCase(Timeout = 120000)]
    [TestCategory("Unit")]
    public async Task FinishedSessionLightsTheButtonForResetAndTheResetPressDarkensIt()
    {
        await RunSession();

        await _rig.RunToCompletion();

        AssertBool(_rig.Button.Enabled).IsTrue();
        AssertBool(_rig.Button.ButtonMesh!.Visible).IsTrue();
        AssertBool(_rig.KindButton.Enabled).IsFalse();

        _rig.Button.Interact();
        await WorkshopSceneRig.Settle();

        AssertBool(_rig.Button.Enabled).IsFalse();
        AssertBool(_rig.Button.ButtonMesh!.Visible).IsFalse();
        AssertBool(_rig.Screen.IsInitialized).IsFalse();
        AssertBool(_rig.KindButton.Enabled).IsTrue();
        AssertThat(_rig.Controller.MapType).IsEqual(MapGenerationType.IrregularMesh);
    }

    [TestCase(Timeout = 120000)]
    [TestCategory("Unit")]
    public async Task SecondSessionRunsAfterTheResetOnceBothHoldersAreFilledAgain()
    {
        await RunSession();
        await _rig.RunToCompletion();
        _rig.Button.Interact();
        await WorkshopSceneRig.Settle();

        await _rig.Place(_rig.DeckHolder, SomeCard);
        AssertBool(_rig.Button.Enabled).IsFalse();

        await _rig.Place(_rig.CardHolder, SomeCard);
        AssertBool(_rig.Button.Enabled).IsTrue();

        _rig.Button.Interact();
        await WorkshopSceneRig.Settle();
        await _rig.WaitForMap();

        AssertBool(_rig.DeckHolder.HasCards).IsFalse();
        AssertBool(_rig.CardHolder.HasCards).IsFalse();
        AssertBool(_rig.Screen.IsInitialized).IsTrue();
    }

    private async Task RunSession()
    {
        await _rig.Place(_rig.CardHolder, SomeCard);
        _rig.Button.Interact();
        await WorkshopSceneRig.Settle();
        await _rig.WaitForMap();
        AssertBool(_rig.Screen.IsInitialized).IsTrue();
    }
}
