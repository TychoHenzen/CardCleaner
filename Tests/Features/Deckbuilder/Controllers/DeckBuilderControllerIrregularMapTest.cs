using System.Reflection;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Components;
using CardCleaner.Scripts.Features.Deckbuilder.Controllers;
using CardCleaner.Scripts.Features.Deckbuilder.Models;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh;
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
    private IrregularWorldMapScreen? _screen;

    private async Task BuildController(bool withIrregularScreen)
    {
        _abilitySlot = CreateSlot("AbilitySlot");
        _mapSlot = CreateSlot("MapSlot");
        _button = new InteractableButton { Name = "ActivateButton" };
        _button.AddChild(new CollisionShape3D { Shape = new BoxShape3D() });

        var controller = new DeckBuilderController
        {
            AbilityDeckSlot = _abilitySlot,
            MapCardSlot = _mapSlot,
            ActivateButton = _button,
            WorldTileMapScreenScene = new SimpleWorldMapScreen(),
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
        AddNode(controller);
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

    private static void RaiseExplorationFinished(IrregularWorldMapScreen screen)
    {
        var backingField = typeof(IrregularWorldMapScreen).GetField(
            nameof(IrregularWorldMapScreen.ExplorationFinished),
            BindingFlags.Instance | BindingFlags.NonPublic);
        ((System.Action?)backingField!.GetValue(screen))?.Invoke();
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
    }

    [TestCase]
    public async Task FinishedIrregularExplorationReenablesTheButtonForReset()
    {
        await BuildController(withIrregularScreen: true);
        await Insert(_abilitySlot);
        await Insert(_mapSlot);
        _button.Interact();
        AssertBool(_button.Enabled).IsFalse();

        RaiseExplorationFinished(_screen!);

        AssertBool(_button.Enabled).IsTrue();
    }
}
