using System;
using System.Threading;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Components;
using CardCleaner.Scripts.Features.Deckbuilder.Controllers;
using CardCleaner.Scripts.Features.Deckbuilder.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Core.Enumeration;
using Godot;

namespace CardCleaner.Tests.Features.Deckbuilder.Controllers;

/// <summary>
/// DeckBuilderController and its map kind toggle on the grid map, driven by the session states a grid session
/// reports: the toggle is lit while the cabinet is idle, a completed session keeps it dark until the reset press,
/// and a session whose generation fails ends at once.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class DeckBuilderControllerGridSessionTest
{
    private DeckSlot _abilitySlot = null!;
    private DeckSlot _mapSlot = null!;
    private InteractableButton _button = null!;
    private InteractableButton _kindButton = null!;
    private Label _statusLabel = null!;
    private FakeGameSession _session = null!;

    [BeforeTest]
    public async Task Setup()
    {
        ServiceLocator.ResetForTesting();
        _session = new FakeGameSession();
        ServiceLocator.Container.RegisterSingleton<IGameSessionService>(_session);
        ServiceLocator.Container.RegisterSingleton<IGridMapGenerator>(new PendingGridGenerator());

        _abilitySlot = CreateSlot("AbilitySlot");
        _mapSlot = CreateSlot("MapSlot");
        _button = CreateButton("ActivateButton");
        _kindButton = CreateButton("MapKindButton");
        _statusLabel = AutoFree(new Label());

        var controller = new DeckBuilderController
        {
            AbilityDeckSlot = _abilitySlot,
            MapCardSlot = _mapSlot,
            ActivateButton = _button,
            MapKindButton = _kindButton,
            // Never added to the tree, so Initialize only stores the session and nothing generates.
            WorldTileMapScreenScene = AutoFree(new SimpleWorldMapScreen { StatusLabel = _statusLabel })
        };

        AddNode(_abilitySlot);
        AddNode(_mapSlot);
        AddNode(_button);
        AddNode(_kindButton);
        AddNode(controller);
        await ISceneRunner.SyncProcessFrame;

        await Insert(_abilitySlot);
        await Insert(_mapSlot);
    }

    [AfterTest]
    public static void Teardown()
    {
        ServiceLocator.ResetForTesting();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CompletedGridSessionKeepsTheToggleDarkUntilTheResetPress()
    {
        AssertBool(_kindButton.Enabled).IsTrue();
        AssertThat(_statusLabel.Text).IsEqual("Map: grid");

        _button.Interact();
        AssertBool(_kindButton.Enabled).IsFalse();

        _session.Report(SessionState.SessionComplete);
        _session.Report(SessionState.WaitingForCards);

        AssertBool(_button.Enabled).IsTrue();
        AssertBool(_kindButton.Enabled).IsFalse();

        // The grid screen's own session status, which the reset press replaces with the picked kind.
        _statusLabel.Text = "Status: SessionComplete";
        _button.Interact();

        AssertBool(_kindButton.Enabled).IsTrue();
        AssertThat(_statusLabel.Text).IsEqual("Map: grid");
    }

    [TestCase]
    [TestCategory("Unit")]
    public void GridSessionThatFallsBackToWaitingWithoutCompletingLightsTheToggleAgain()
    {
        _button.Interact();
        _session.Report(SessionState.GeneratingMap);
        AssertBool(_kindButton.Enabled).IsFalse();

        _session.Report(SessionState.WaitingForCards);

        AssertBool(_kindButton.Enabled).IsTrue();
        AssertBool(_button.Enabled).IsFalse();
    }

    private static DeckSlot CreateSlot(string name)
    {
        var slot = new DeckSlot { Name = name, Area = new Area3D { Name = "Area" } };
        slot.AddChild(slot.Area);
        return slot;
    }

    private static InteractableButton CreateButton(string name)
    {
        var button = new InteractableButton { Name = name };
        button.AddChild(new CollisionShape3D { Shape = new BoxShape3D() });
        return button;
    }

    private static async Task Insert(DeckSlot slot)
    {
        var card = new CardController { Name = "Card", Signature = new CardSignature().With(Element.Febris, 0.4f) };
        card.AddChild(new CollisionShape3D { Shape = new BoxShape3D() });
        AddNode(card);
        slot.Area.EmitSignal(Area3D.SignalName.BodyEntered, card);
        await ISceneRunner.SyncProcessFrame;
        await ISceneRunner.SyncProcessFrame;
    }

    /// <summary>A grid generator whose map never arrives; the controller only needs one registered.</summary>
    private sealed class PendingGridGenerator : IGridMapGenerator
    {
        public Task<IGeneratedMap> GenerateAsync(
            MapGenerationConfig config,
            IProgress<float>? progress = null,
            CancellationToken cancellationToken = default) => new TaskCompletionSource<IGeneratedMap>().Task;
    }
}
