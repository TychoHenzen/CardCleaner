using System.Threading.Tasks;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Deckbuilder.Components;
using CardCleaner.Scripts.Features.Deckbuilder.Controllers;
using CardCleaner.Scripts.Features.Deckbuilder.Models;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh;
using CardCleaner.Tests.Features.Worldgen.IrregularMesh;
using Godot;

namespace CardCleaner.Tests.Features.Deckbuilder.Controllers;

/// <summary>
/// DeckBuilderController behaviour when MapType selects the irregular mesh map.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class DeckBuilderControllerIrregularMapTest
{
    private DeckSlot _abilitySlot = null!;
    private DeckSlot _mapSlot = null!;
    private InteractableButton _button = null!;
    private InteractableButton _kindButton = null!;
    private DeckBuilderController _controller = null!;
    private IrregularWorldMapScreen? _screen;

    // The cabinet needs a registered irregular generator to start; this one never finishes, so no map is generated.
    [BeforeTest]
    public static void RegisterServices()
    {
        ServiceLocator.ResetForTesting();
        ServiceLocator.Container.RegisterSingleton<IMeshMapGenerator>(MeshMapGeneratorFixture.Pending());
    }

    [AfterTest]
    public static void ClearServices() => ServiceLocator.ResetForTesting();

    private async Task BuildController(bool withIrregularScreen)
    {
        _abilitySlot = CreateSlot("AbilitySlot");
        _mapSlot = CreateSlot("MapSlot");
        _button = new InteractableButton { Name = "ActivateButton" };
        _button.AddChild(new CollisionShape3D { Shape = new BoxShape3D() });
        _kindButton = new InteractableButton { Name = "MapKindButton" };
        _kindButton.AddChild(new CollisionShape3D { Shape = new BoxShape3D() });

        var controller = new DeckBuilderController
        {
            AbilityDeckSlot = _abilitySlot,
            MapCardSlot = _mapSlot,
            ActivateButton = _button,
            MapKindButton = _kindButton,
            // Never added to the tree: only the irregular map type is under test.
            WorldTileMapScreenScene = AutoFree(new SimpleWorldMapScreen()),
            MapType = MapGenerationType.IrregularMesh
        };

        if (withIrregularScreen)
        {
            var viewport = new SubViewport { Name = "Viewport" };
            _screen = new IrregularWorldMapScreen { MeshRings = 2, Viewport = viewport };
            _screen.AddChild(viewport);
            AddNode(_screen);
            controller.IrregularMapScreen = _screen;
        }

        AddNode(_abilitySlot);
        AddNode(_mapSlot);
        AddNode(_button);
        AddNode(_kindButton);
        AddNode(controller);
        _controller = controller;
        await ISceneRunner.SyncProcessFrame;
    }

    private static DeckSlot CreateSlot(string name)
    {
        var slot = new DeckSlot { Name = name, Area = new Area3D { Name = "Area" } };
        slot.AddChild(slot.Area);
        return slot;
    }

    private static async Task Insert(DeckSlot slot)
    {
        var card = new CardController
        {
            Name = "Card",
            Signature = new CardSignature(new[] { 0.5f, -0.3f, 0.8f, 0f, 0f, 0f, 0f, 0f })
        };
        card.AddChild(new CollisionShape3D { Shape = new BoxShape3D() });
        AddNode(card);
        slot.Area.EmitSignal(Area3D.SignalName.BodyEntered, card);
        await ISceneRunner.SyncProcessFrame;
        await ISceneRunner.SyncProcessFrame;
    }

    [TestCase]
    public async Task MissingIrregularScreenDoesNotConsumeTheSelectedCards()
    {
        await BuildController(withIrregularScreen: false);
        await Insert(_abilitySlot);
        await Insert(_mapSlot);

        _button.Interact();

        AssertBool(_abilitySlot.HasCards).IsTrue();
        AssertBool(_mapSlot.HasCard).IsTrue();
        AssertBool(_kindButton.Enabled).IsTrue();
    }

    [TestCase]
    public async Task FinishedIrregularExplorationReenablesTheButtonForReset()
    {
        await BuildController(withIrregularScreen: true);
        await Insert(_abilitySlot);
        await Insert(_mapSlot);
        _button.Interact();
        AssertBool(_button.Enabled).IsFalse();

        _screen!.RaiseExplorationFinished();

        AssertBool(_button.Enabled).IsTrue();
    }

    [TestCase]
    public async Task FinishedExplorationIsHandledForTheKindThatRanEvenIfMapTypeChangedSince()
    {
        await BuildController(withIrregularScreen: true);
        await Insert(_abilitySlot);
        await Insert(_mapSlot);
        _button.Interact();

        _controller.MapType = MapGenerationType.RegularGrid;
        _screen!.RaiseExplorationFinished();

        AssertBool(_button.Enabled).IsTrue();
    }

    [TestCase]
    public async Task KindToggleStaysDarkFromTheActivatePressUntilTheResetPress()
    {
        await BuildController(withIrregularScreen: true);
        AssertBool(_kindButton.Enabled).IsTrue();
        await Insert(_abilitySlot);
        await Insert(_mapSlot);

        _button.Interact();
        AssertBool(_kindButton.Enabled).IsFalse();

        _kindButton.EmitSignal(InteractableButton.SignalName.ButtonPressed);
        AssertThat(_controller.MapType).IsEqual(MapGenerationType.IrregularMesh);

        _screen!.RaiseExplorationFinished();
        AssertBool(_kindButton.Enabled).IsFalse();

        _button.Interact();
        AssertBool(_kindButton.Enabled).IsTrue();
        AssertBool(_screen.IsInitialized).IsFalse();
    }
}
